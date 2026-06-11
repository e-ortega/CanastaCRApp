using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Api.Services;

public class PriceService(AppDbContext db)
{
    private static readonly TimeSpan PriceExpiry = TimeSpan.FromDays(90);

    public async Task<ProductPriceComparisonDto?> GetComparisonAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await db.Products.FindAsync([productId], ct);
        if (product is null) return null;

        var now = DateTimeOffset.UtcNow;

        var prices = await db.PriceReports
            .Include(r => r.Store)
            .Where(r => r.ProductId == productId)
            .GroupBy(r => r.StoreId)
            .Select(g => g.OrderByDescending(r => r.ReportedAt).First())
            .ToListAsync(ct);

        var storePrices = prices
            .Select(r => new StorePriceDto(
                r.StoreId,
                r.Store.Name,
                r.Store.Chain,
                r.Price,
                r.Currency,
                r.ReportedAt,
                r.ExpiresAt < now))
            .OrderBy(p => p.Price)
            .ToList();

        var activePrices = storePrices.Where(p => !p.IsExpired).ToList();
        decimal? lowest = activePrices.Count > 0 ? activePrices.Min(p => p.Price) : null;
        decimal? highest = activePrices.Count > 0 ? activePrices.Max(p => p.Price) : null;
        decimal? savings = highest - lowest;
        decimal? savingsPct = highest > 0 ? savings / highest * 100 : null;

        return new ProductPriceComparisonDto(
            product.Id,
            product.Name,
            product.ImageUrl,
            storePrices,
            lowest,
            highest,
            savings,
            savingsPct);
    }

    public async Task<PriceReportDto> ReportAsync(
        CreatePriceReportDto dto, Guid? userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var report = new PriceReport
        {
            Id = Guid.NewGuid(),
            ProductId = dto.ProductId,
            StoreId = dto.StoreId,
            Price = dto.Price,
            Currency = dto.Currency,
            Source = userId.HasValue ? PriceSource.UserSubmitted : PriceSource.Scraped,
            ReportedById = userId,
            ReportedAt = now,
            ExpiresAt = now.Add(PriceExpiry)
        };

        db.PriceReports.Add(report);

        if (userId.HasValue)
        {
            var user = await db.Users.FindAsync([userId.Value], ct);
            if (user is not null) user.ReputationPoints += 5;
        }

        await db.SaveChangesAsync(ct);

        var saved = await db.PriceReports
            .Include(r => r.Product)
            .Include(r => r.Store)
            .FirstAsync(r => r.Id == report.Id, ct);

        return MapToDto(saved, now);
    }

    public async Task<List<PriceReportDto>> GetRecentForStoreAsync(
        Guid storeId, int page, int pageSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.PriceReports
            .Include(r => r.Product)
            .Include(r => r.Store)
            .Where(r => r.StoreId == storeId)
            .OrderByDescending(r => r.ReportedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(r => MapToDto(r, now))
            .ToListAsync(ct);
    }

    public async Task<SavingsSummaryDto> GetSavingsSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // For each product that has active prices at 2+ stores, compute the gap
        var productPrices = await db.PriceReports
            .Include(r => r.Store)
            .Where(r => r.ExpiresAt > now)
            .GroupBy(r => new { r.ProductId, r.StoreId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.StoreId,
                Price = g.OrderByDescending(r => r.ReportedAt).First().Price,
                StoreName = g.OrderByDescending(r => r.ReportedAt).First().Store.Name,
            })
            .ToListAsync(ct);

        var productGroups = productPrices
            .GroupBy(p => p.ProductId)
            .Where(g => g.Count() >= 2)
            .ToList();

        var productNames = await db.Products
            .Where(p => productGroups.Select(g => g.Key).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var deals = new List<TopDealDto>();
        foreach (var group in productGroups)
        {
            var min = group.OrderBy(p => p.Price).First();
            var max = group.OrderByDescending(p => p.Price).First();
            var savingsAmt = max.Price - min.Price;
            var savingsPct = max.Price > 0 ? savingsAmt / max.Price * 100 : 0;

            deals.Add(new TopDealDto(
                group.Key,
                productNames.GetValueOrDefault(group.Key, ""),
                min.StoreName,
                min.Price,
                max.StoreName,
                max.Price,
                savingsAmt,
                savingsPct));
        }

        deals.Sort((a, b) => b.SavingsPercent.CompareTo(a.SavingsPercent));

        return new SavingsSummaryDto(
            TotalProducts: productNames.Count,
            ProductsWithPriceGap: deals.Count,
            TotalPotentialSavings: deals.Sum(d => d.SavingsAmount),
            AvgSavingsPercent: deals.Count > 0 ? deals.Average(d => d.SavingsPercent) : 0,
            TopDeals: deals.Take(5).ToList());
    }

    private static PriceReportDto MapToDto(PriceReport r, DateTimeOffset now) =>
        new(r.Id, r.ProductId, r.Product.Name, r.StoreId, r.Store.Name, r.Store.Chain,
            r.Price, r.Currency, r.Source, r.ReportedAt, r.ExpiresAt, r.ExpiresAt < now);
}

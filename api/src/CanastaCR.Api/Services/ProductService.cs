using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Interfaces;
using CanastaCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Api.Services;

public class ProductService(AppDbContext db, IOpenFoodFactsClient offClient)
{
    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Products.FindAsync([id], ct);
        return p is null ? null : MapToDto(p);
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.Barcode == barcode, ct);
        return p is null ? null : MapToDto(p);
    }

    public async Task<List<ProductSearchResultDto>> GetAllAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await db.Products
            .OrderBy(p => p.Name)
            .Select(p => new ProductSearchResultDto(
                p.Id,
                p.Barcode,
                p.Name,
                p.Brand,
                p.Category,
                p.ImageUrl,
                p.PriceReports
                    .Where(r => r.ExpiresAt > now)
                    .OrderBy(r => r.Price)
                    .Select(r => (decimal?)r.Price)
                    .FirstOrDefault(),
                p.PriceReports
                    .Where(r => r.ExpiresAt > now)
                    .OrderBy(r => r.Price)
                    .Select(r => r.Store.Name)
                    .FirstOrDefault()
            ))
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<List<ProductSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await db.Products
            .Where(p => p.Name.ToLower().Contains(query.ToLower()) ||
                        (p.Brand != null && p.Brand.ToLower().Contains(query.ToLower())))
            .Select(p => new ProductSearchResultDto(
                p.Id,
                p.Barcode,
                p.Name,
                p.Brand,
                p.Category,
                p.ImageUrl,
                p.PriceReports
                    .Where(r => r.ExpiresAt > now)
                    .OrderBy(r => r.Price)
                    .Select(r => (decimal?)r.Price)
                    .FirstOrDefault(),
                p.PriceReports
                    .Where(r => r.ExpiresAt > now)
                    .OrderBy(r => r.Price)
                    .Select(r => r.Store.Name)
                    .FirstOrDefault()
            ))
            .Take(20)
            .ToListAsync(ct);
    }

    public async Task<ProductDto> LookupOrCreateByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var existing = await db.Products.FirstOrDefaultAsync(p => p.Barcode == barcode, ct);
        if (existing is not null) return MapToDto(existing);

        var offData = await offClient.LookupByBarcodeAsync(barcode, ct);
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Barcode = barcode,
            Name = offData?.Name ?? "Unknown product",
            Brand = offData?.Brand,
            Category = offData?.Category,
            ImageUrl = offData?.ImageUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return MapToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Barcode = dto.Barcode,
            Name = dto.Name,
            Brand = dto.Brand,
            Category = dto.Category,
            ImageUrl = dto.ImageUrl,
            Description = dto.Description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return MapToDto(product);
    }

    private static ProductDto MapToDto(Product p) =>
        new(p.Id, p.Barcode, p.Name, p.Brand, p.Category, p.ImageUrl, p.Description);
}

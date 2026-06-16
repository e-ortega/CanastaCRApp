using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Core.Interfaces;
using CanastaCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Api.Services;

public class ProductService(AppDbContext db, IOpenFoodFactsClient offClient)
{
    // Shown to the user as the product name when Open Food Facts has nothing for this
    // barcode. Kept as a known constant (not just an inline literal) so the Flutter app can
    // detect it and prompt the user to fix the name before reporting a price.
    public const string UnnamedProductPlaceholder = "Producto sin nombre";

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Products.FindAsync([id], ct);
        return p is null ? null : MapToDto(p);
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var p = await FindByBarcodeOrAlternateAsync(barcode, ct);
        return p is null ? null : MapToDto(p);
    }

    private async Task<Product?> FindByBarcodeOrAlternateAsync(string barcode, CancellationToken ct)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.Barcode == barcode, ct);
        if (p is not null) return p;

        var alt = AlternateBarcodeForm(barcode);
        return alt is null ? null : await db.Products.FirstOrDefaultAsync(x => x.Barcode == alt, ct);
    }

    /// <summary>
    /// A 12-digit UPC-A code and its 13-digit EAN-13 form (UPC-A with a leading zero) are the
    /// same product, but different scanners/sources don't agree on which form they report —
    /// a common, well-documented gap in barcode lookups (Open Food Facts has the same
    /// ambiguity internally). Returns the other form, or null if the input isn't a barcode
    /// this conversion applies to.
    /// </summary>
    public static string? AlternateBarcodeForm(string barcode)
    {
        if (barcode.Length == 12 && barcode.All(char.IsAsciiDigit)) return "0" + barcode;
        if (barcode.Length == 13 && barcode[0] == '0' && barcode.All(char.IsAsciiDigit)) return barcode[1..];
        return null;
    }

    public async Task<List<ProductSearchResultDto>> GetAllAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var raw = await db.Products
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Barcode, p.Name, p.Brand, p.Category, p.ImageUrl,
                Cheapest = p.PriceReports
                    .Where(r => r.ExpiresAt > now)
                    .OrderBy(r => r.Price)
                    .Select(r => new { r.Price, StoreName = r.Store == null ? null : r.Store.Name, r.Chain })
                    .FirstOrDefault()
            })
            .Take(50)
            .ToListAsync(ct);

        return raw.Select(p => new ProductSearchResultDto(
            p.Id, p.Barcode, p.Name, p.Brand, p.Category, p.ImageUrl,
            p.Cheapest?.Price,
            LowestPriceStoreLabel(p.Cheapest?.StoreName, p.Cheapest?.Chain)
        )).ToList();
    }

    public async Task<List<ProductSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var raw = await db.Products
            .Where(p => p.Name.ToLower().Contains(query.ToLower()) ||
                        (p.Brand != null && p.Brand.ToLower().Contains(query.ToLower())))
            .Select(p => new
            {
                p.Id, p.Barcode, p.Name, p.Brand, p.Category, p.ImageUrl,
                Cheapest = p.PriceReports
                    .Where(r => r.ExpiresAt > now)
                    .OrderBy(r => r.Price)
                    .Select(r => new { r.Price, StoreName = r.Store == null ? null : r.Store.Name, r.Chain })
                    .FirstOrDefault()
            })
            .Take(20)
            .ToListAsync(ct);

        return raw.Select(p => new ProductSearchResultDto(
            p.Id, p.Barcode, p.Name, p.Brand, p.Category, p.ImageUrl,
            p.Cheapest?.Price,
            LowestPriceStoreLabel(p.Cheapest?.StoreName, p.Cheapest?.Chain)
        )).ToList();
    }

    // Chain-name fallback can't run inside the EF query above (custom extension methods
    // aren't SQL-translatable) — apply it after materialization instead.
    private static string? LowestPriceStoreLabel(string? storeName, StoreChain? chain) =>
        storeName ?? chain?.GetDisplayName();

    public async Task<ProductDto> LookupOrCreateByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var existing = await FindByBarcodeOrAlternateAsync(barcode, ct);
        if (existing is not null) return MapToDto(existing);

        var offData = await offClient.LookupByBarcodeAsync(barcode, ct);
        var alt = AlternateBarcodeForm(barcode);
        if (offData is null && alt is not null)
            offData = await offClient.LookupByBarcodeAsync(alt, ct);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            // Keep the form the device actually scanned — a later scan from a different device
            // that reports the other form will still resolve to this same product via
            // FindByBarcodeOrAlternateAsync, so this never creates a duplicate going forward.
            Barcode = barcode,
            Name = offData?.Name ?? UnnamedProductPlaceholder,
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

    public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductDto dto, CancellationToken ct = default)
    {
        var product = await db.Products.FindAsync([id], ct);
        if (product is null) return null;

        product.Name = dto.Name;

        await db.SaveChangesAsync(ct);
        return MapToDto(product);
    }

    private static ProductDto MapToDto(Product p) =>
        new(p.Id, p.Barcode, p.Name, p.Brand, p.Category, p.ImageUrl, p.Description);
}

using CanastaCR.Core.Entities;
using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Persistence;
using CanastaCR.Scraper.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanastaCR.Scraper.Tests.Services;

/// <summary>
/// Uses SQLite (a real relational provider) rather than EF's InMemory provider, because
/// InMemory does not enforce LINQ-to-SQL translation rules and would not have caught the
/// production incident where FindOrCreateProductAsync called a non-translatable C# method
/// (Normalize) inside a Where() predicate, throwing InvalidOperationException on every
/// barcode-less product (see docs/ARCHITECTURE.md section 7).
/// </summary>
public class ProductMatcherServiceMatchingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ScraperDbContext _db;
    private readonly ProductMatcherService _matcher;

    public ProductMatcherServiceMatchingTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ScraperDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ScraperDbContext(options);
        _db.Database.EnsureCreated();
        _matcher = new ProductMatcherService(_db, NullLogger<ProductMatcherService>.Instance);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task FindOrCreateProductAsync_DoesNotThrow_WhenBarcodeIsNull()
    {
        var scraped = new ScrapedProduct(
            Name: "Aceite Girasol 1L", Brand: "La Favorita", Barcode: null,
            Price: 1500, Currency: "CRC", ImageUrl: null, Category: null,
            IsAvailable: true, SourceUrl: "https://example.com");

        var product = await _matcher.FindOrCreateProductAsync(scraped, CancellationToken.None);

        Assert.NotNull(product);
        Assert.Equal("Aceite Girasol 1L", product.Name);
    }

    [Fact]
    public async Task FindOrCreateProductAsync_MatchesExisting_WhenBarcodeIsNullButNameIsSimilar()
    {
        _db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            Name = "ACEITE DE GIRASOL LA FAVORITA 1 LT",
            Brand = "La Favorita",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();

        var scraped = new ScrapedProduct(
            Name: "Aceite Girasol La Favorita 1L", Brand: "La Favorita", Barcode: null,
            Price: 1500, Currency: "CRC", ImageUrl: null, Category: null,
            IsAvailable: true, SourceUrl: "https://example.com");

        var product = await _matcher.FindOrCreateProductAsync(scraped, CancellationToken.None);

        Assert.Equal("ACEITE DE GIRASOL LA FAVORITA 1 LT", product.Name);
        Assert.Equal(1, await _db.Products.CountAsync());
    }

    [Fact]
    public async Task FindOrCreateProductAsync_CreatesNewProduct_WhenBarcodeMatchesExisting()
    {
        var existing = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Leche Dos Pinos 1L",
            Barcode = "7441001000001",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Products.Add(existing);
        await _db.SaveChangesAsync();

        var scraped = new ScrapedProduct(
            Name: "Leche Dos Pinos 1L", Brand: "Dos Pinos", Barcode: "7441001000001",
            Price: 870, Currency: "CRC", ImageUrl: null, Category: null,
            IsAvailable: true, SourceUrl: "https://example.com");

        var product = await _matcher.FindOrCreateProductAsync(scraped, CancellationToken.None);

        Assert.Equal(existing.Id, product.Id);
        Assert.Equal(1, await _db.Products.CountAsync());
    }

    [Fact]
    public async Task CreateProductAsync_ReturnsTheWinner_WhenAnotherStoreInsertedTheSameBarcodeConcurrently()
    {
        // Simulates the actual production incident (2026-06-16, ARCHITECTURE.md section 7):
        // two stores scrape concurrently, each on its own DbContext. Both check "does this
        // barcode exist?" before either has inserted it, both get null, and both then try to
        // insert — only one wins the unique constraint. CreateProductAsync is called directly
        // here (bypassing FindOrCreateProductAsync's own check) to deterministically recreate
        // "we already decided to create, but meanwhile someone else won the race" without
        // needing real thread concurrency, which the actual race depends on timing for.
        var scraped = new ScrapedProduct(
            Name: "Coca-Cola 2L", Brand: "Coca-Cola", Barcode: "7441099999999",
            Price: 1200, Currency: "CRC", ImageUrl: null, Category: null,
            IsAvailable: true, SourceUrl: "https://example.com");

        var winner = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Coca-Cola 2L",
            Barcode = "7441099999999",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Products.Add(winner);
        await _db.SaveChangesAsync(); // the "other store" already committed this insert

        var result = await _matcher.CreateProductAsync(scraped, CancellationToken.None);

        Assert.Equal(winner.Id, result.Id);
        Assert.Equal(1, await _db.Products.CountAsync()); // no duplicate row, despite the conflict
    }
}

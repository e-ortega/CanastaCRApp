using CanastaCR.Api.Services;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Tests.Helpers;

namespace CanastaCR.Tests.Services;

public class PriceServiceTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid Store1Id  = Guid.NewGuid();
    private static readonly Guid Store2Id  = Guid.NewGuid();
    private static readonly Guid Store3Id  = Guid.NewGuid();

    private static void SeedBase(CanastaCR.Infrastructure.Persistence.AppDbContext db)
    {
        db.Products.Add(new Product
        {
            Id = ProductId, Name = "Leche Dos Pinos 1L", CreatedAt = DateTimeOffset.UtcNow
        });
        db.Stores.AddRange(
            new Store { Id = Store1Id, Name = "AutoMercado", Chain = StoreChain.AutoMercado },
            new Store { Id = Store2Id, Name = "MaxiPalí",    Chain = StoreChain.MaxiPali    },
            new Store { Id = Store3Id, Name = "MegaSuper",   Chain = StoreChain.MegaSuper   }
        );
        db.SaveChanges();
    }

    private static void AddPrice(CanastaCR.Infrastructure.Persistence.AppDbContext db,
        Guid storeId, decimal price, DateTimeOffset? reportedAt = null, DateTimeOffset? expiresAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        db.PriceReports.Add(new PriceReport
        {
            Id           = Guid.NewGuid(),
            ProductId    = ProductId,
            StoreId      = storeId,
            Price        = price,
            Currency     = "CRC",
            Source       = PriceSource.UserSubmitted,
            ReportedAt   = reportedAt ?? now,
            ExpiresAt    = expiresAt ?? now.AddDays(90),
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task GetComparison_ReturnsPricesOrderedCheapestFirst()
    {
        var db = DbContextFactory.Create();
        SeedBase(db);
        AddPrice(db, Store1Id, 1250m);
        AddPrice(db, Store2Id, 975m);
        AddPrice(db, Store3Id, 1065m);

        var service = new PriceService(db);
        var result  = await service.GetComparisonAsync(ProductId);

        Assert.NotNull(result);
        Assert.Equal(975m,  result.LowestPrice);
        Assert.Equal(1250m, result.HighestPrice);
        Assert.Equal(result.Prices[0].Price, result.Prices.Min(p => p.Price));
    }

    [Fact]
    public async Task GetComparison_CalculatesSavingsAmountAndPercent()
    {
        var db = DbContextFactory.Create();
        SeedBase(db);
        AddPrice(db, Store1Id, 1000m);
        AddPrice(db, Store2Id, 800m);

        var service = new PriceService(db);
        var result  = await service.GetComparisonAsync(ProductId);

        Assert.Equal(200m, result!.SavingsAmount);
        Assert.Equal(20m,  result.SavingsPercent);
    }

    [Fact]
    public async Task GetComparison_ExcludesExpiredPrices_FromSavingsCalc()
    {
        var db = DbContextFactory.Create();
        SeedBase(db);
        // One active, one expired (very cheap but shouldn't affect active savings)
        AddPrice(db, Store1Id, 1000m);
        AddPrice(db, Store2Id, 100m, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var service = new PriceService(db);
        var result  = await service.GetComparisonAsync(ProductId);

        // Only the active price drives savings calc
        Assert.Equal(1000m, result!.LowestPrice);
        Assert.Equal(1000m, result.HighestPrice);
        Assert.Equal(0m,    result.SavingsAmount);
    }

    [Fact]
    public async Task GetComparison_ReturnsNull_WhenProductDoesNotExist()
    {
        var db      = DbContextFactory.Create();
        var service = new PriceService(db);

        var result = await service.GetComparisonAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSavingsSummary_ReturnsDealsForProductsWithMultipleStorePrices()
    {
        var db = DbContextFactory.Create();
        SeedBase(db);
        AddPrice(db, Store1Id, 1250m);
        AddPrice(db, Store2Id, 975m);

        var service = new PriceService(db);
        var summary = await service.GetSavingsSummaryAsync();

        Assert.Equal(1, summary.ProductsWithPriceGap);
        Assert.Equal(275m, summary.TotalPotentialSavings);
        Assert.Single(summary.TopDeals);
        Assert.Equal("MaxiPalí",    summary.TopDeals[0].CheapestStore);
        Assert.Equal("AutoMercado", summary.TopDeals[0].MostExpensiveStore);
    }

    [Fact]
    public async Task GetSavingsSummary_ExcludesExpiredPrices()
    {
        var db = DbContextFactory.Create();
        SeedBase(db);
        AddPrice(db, Store1Id, 1250m, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        AddPrice(db, Store2Id, 975m,  expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var service = new PriceService(db);
        var summary = await service.GetSavingsSummaryAsync();

        Assert.Equal(0, summary.ProductsWithPriceGap);
    }
}

using CanastaCR.Api.Controllers;
using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace CanastaCR.Tests.Controllers;

public class PricesControllerTests
{
    private PricesController CreateController(PriceService? priceService = null)
    {
        priceService ??= new PriceService(DbContextFactory.Create());
        return new PricesController(priceService);
    }

    [Fact]
    public async Task GetSavings_ReturnsOkResult_WithSummaryData()
    {
        var db = DbContextFactory.Create();
        var product = new Product { Id = Guid.NewGuid(), Name = "Item", Brand = "B", Category = "C", CreatedAt = DateTimeOffset.UtcNow };
        var store1 = new Store { Id = Guid.NewGuid(), Name = "S1", Chain = StoreChain.AutoMercado, City = "SJ", Lat = 0, Lng = 0 };
        var store2 = new Store { Id = Guid.NewGuid(), Name = "S2", Chain = StoreChain.MasXMenos, City = "SJ", Lat = 0, Lng = 0 };

        product.PriceReports =
        [
            new() { Price = 3000, StoreId = store1.Id, Store = store1, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Source = PriceSource.UserSubmitted },
            new() { Price = 2500, StoreId = store2.Id, Store = store2, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Source = PriceSource.UserSubmitted }
        ];

        db.Products.Add(product);
        db.Stores.AddRange(store1, store2);
        await db.SaveChangesAsync();

        var priceService = new PriceService(db);
        var controller = CreateController(priceService);

        var result = await controller.GetSavings(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetSavings_ReturnsOkResult_WhenNoPricesExist()
    {
        var controller = CreateController();
        var result = await controller.GetSavings(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<SavingsSummaryDto>(okResult.Value);
        Assert.Equal(0, summary.ProductsWithPriceGap);
    }

    [Fact]
    public async Task GetForStore_ReturnsOkResult_WithStorePrices()
    {
        var db = DbContextFactory.Create();
        var storeId = Guid.NewGuid();
        var store = new Store { Id = storeId, Name = "Store", Chain = StoreChain.AutoMercado, City = "SJ", Lat = 0, Lng = 0 };
        var product = new Product { Id = Guid.NewGuid(), Name = "Item", Brand = "B", Category = "C", CreatedAt = DateTimeOffset.UtcNow };
        product.PriceReports =
        [
            new() { Price = 2000, StoreId = storeId, Store = store, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Source = PriceSource.UserSubmitted }
        ];

        db.Products.Add(product);
        db.Stores.Add(store);
        await db.SaveChangesAsync();

        var priceService = new PriceService(db);
        var controller = CreateController(priceService);

        var result = await controller.GetForStore(storeId, 0, 20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetForStore_ReturnsEmptyList_WhenStoreHasNoPrices()
    {
        var storeId = Guid.NewGuid();
        var priceService = new PriceService(DbContextFactory.Create());
        var controller = CreateController(priceService);

        var result = await controller.GetForStore(storeId, 0, 20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}


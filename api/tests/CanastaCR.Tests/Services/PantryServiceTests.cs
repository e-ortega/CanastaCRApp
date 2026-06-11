using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Tests.Helpers;

namespace CanastaCR.Tests.Services;

public class PantryServiceTests
{
    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    private static CanastaCR.Infrastructure.Persistence.AppDbContext SeedBase()
    {
        var db = DbContextFactory.Create();
        db.Users.Add(new User
        {
            Id = UserId, Email = "test@test.com", DisplayName = "Test",
            PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow
        });
        db.Products.Add(new Product
        {
            Id = ProductId, Name = "Arroz Tío Pelón 1kg", CreatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Upsert_CreatesNewPantryItem_WhenProductExists()
    {
        var db      = SeedBase();
        var service = new PantryService(db);

        var result = await service.UpsertAsync(UserId, new UpsertPantryItemDto(ProductId, 2m));

        Assert.NotNull(result);
        Assert.Equal("Arroz Tío Pelón 1kg", result.ProductName);
        Assert.Equal(2m, result.Quantity);
        Assert.Single(db.PantryItems);
    }

    [Fact]
    public async Task Upsert_UpdatesQuantity_WhenItemAlreadyExists()
    {
        var db      = SeedBase();
        var service = new PantryService(db);

        await service.UpsertAsync(UserId, new UpsertPantryItemDto(ProductId, 2m));
        var result = await service.UpsertAsync(UserId, new UpsertPantryItemDto(ProductId, 5m, MinThreshold: 3m));

        Assert.NotNull(result);
        Assert.Equal(5m, result.Quantity);
        Assert.Equal(3m, result.MinThreshold);
        Assert.Single(db.PantryItems); // Still only one item, not duplicated
    }

    [Fact]
    public async Task Upsert_ReturnsNull_WhenProductDoesNotExist()
    {
        var db      = SeedBase();
        var service = new PantryService(db);

        var result = await service.UpsertAsync(UserId, new UpsertPantryItemDto(Guid.NewGuid(), 1m));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetItems_MarksItemAsRunningLow_WhenQuantityBelowThreshold()
    {
        var db = SeedBase();
        db.PantryItems.Add(new PantryItem
        {
            Id = Guid.NewGuid(), UserId = UserId, ProductId = ProductId,
            Quantity = 0.5m, MinThreshold = 2m, Unit = QuantityUnit.Unit,
            UpdatedAt = DateTimeOffset.UtcNow, Product = db.Products.Find(ProductId)!
        });
        db.SaveChanges();

        var service = new PantryService(db);
        var items   = await service.GetItemsAsync(UserId);

        Assert.Single(items);
        Assert.True(items[0].IsRunningLow);
    }

    [Fact]
    public async Task GetItems_DoesNotMarkRunningLow_WhenQuantityAtOrAboveThreshold()
    {
        var db = SeedBase();
        db.PantryItems.Add(new PantryItem
        {
            Id = Guid.NewGuid(), UserId = UserId, ProductId = ProductId,
            Quantity = 3m, MinThreshold = 2m, Unit = QuantityUnit.Unit,
            UpdatedAt = DateTimeOffset.UtcNow, Product = db.Products.Find(ProductId)!
        });
        db.SaveChanges();

        var service = new PantryService(db);
        var items   = await service.GetItemsAsync(UserId);

        Assert.False(items[0].IsRunningLow);
    }

    [Fact]
    public async Task Delete_RemovesItem_WhenOwnerRequests()
    {
        var db = SeedBase();
        var itemId = Guid.NewGuid();
        db.PantryItems.Add(new PantryItem
        {
            Id = itemId, UserId = UserId, ProductId = ProductId,
            Quantity = 1m, MinThreshold = 1m, Unit = QuantityUnit.Unit,
            UpdatedAt = DateTimeOffset.UtcNow, Product = db.Products.Find(ProductId)!
        });
        db.SaveChanges();

        var service = new PantryService(db);
        var ok      = await service.DeleteAsync(UserId, itemId);

        Assert.True(ok);
        Assert.Empty(db.PantryItems);
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenItemBelongsToDifferentUser()
    {
        var db     = SeedBase();
        var itemId = Guid.NewGuid();
        db.PantryItems.Add(new PantryItem
        {
            Id = itemId, UserId = Guid.NewGuid(), ProductId = ProductId,
            Quantity = 1m, MinThreshold = 1m, Unit = QuantityUnit.Unit,
            UpdatedAt = DateTimeOffset.UtcNow, Product = db.Products.Find(ProductId)!
        });
        db.SaveChanges();

        var service = new PantryService(db);
        var ok      = await service.DeleteAsync(UserId, itemId);

        Assert.False(ok);
        Assert.Single(db.PantryItems); // Not deleted
    }
}

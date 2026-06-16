using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Tests.Helpers;

namespace CanastaCR.Tests.Services;

public class ShoppingServiceTests
{
    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid StoreA   = Guid.NewGuid();
    private static readonly Guid StoreB   = Guid.NewGuid();
    private static readonly Guid Product1 = Guid.NewGuid();
    private static readonly Guid Product2 = Guid.NewGuid();

    // Returns DB name so each test gets an isolated, seeded in-memory DB.
    // A fresh context is created for the service to avoid change-tracking conflicts.
    private static string SeedBase(decimal travelThreshold = 2000m, int maxStores = 2)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = DbContextFactory.Create(dbName);

        db.Users.Add(new User
        {
            Id = UserId, Email = "test@test.com", DisplayName = "Test",
            PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow
        });
        db.UserPreferences.Add(new UserPreferences
        {
            UserId = UserId,
            TravelCostThreshold = travelThreshold,
            MaxStoresPerTrip    = maxStores
        });
        db.Stores.AddRange(
            new Store { Id = StoreA, Name = "MaxiPalí",    Chain = StoreChain.MaxiPali    },
            new Store { Id = StoreB, Name = "AutoMercado", Chain = StoreChain.AutoMercado }
        );
        db.Products.AddRange(
            new Product { Id = Product1, Name = "Arroz 1kg", CreatedAt = DateTimeOffset.UtcNow },
            new Product { Id = Product2, Name = "Leche 1L",  CreatedAt = DateTimeOffset.UtcNow }
        );
        db.SaveChanges();
        return dbName;
    }

    private static void AddPrice(string dbName, Guid productId, Guid storeId, decimal price)
    {
        using var db = DbContextFactory.Create(dbName);
        db.PriceReports.Add(new PriceReport
        {
            Id        = Guid.NewGuid(), ProductId = productId, StoreId = storeId,
            Price     = price, Currency = "CRC", Source = PriceSource.UserSubmitted,
            ReportedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(90)
        });
        db.SaveChanges();
    }

    // Scraped prices are chain-level, not tied to one Store row — see docs/ARCHITECTURE.md
    // section 11. StoreId is null; Chain is set instead.
    private static void AddChainPrice(string dbName, Guid productId, StoreChain chain, decimal price)
    {
        using var db = DbContextFactory.Create(dbName);
        db.PriceReports.Add(new PriceReport
        {
            Id        = Guid.NewGuid(), ProductId = productId, StoreId = null, Chain = chain,
            Price     = price, Currency = "CRC", Source = PriceSource.Scraped,
            ReportedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(3)
        });
        db.SaveChanges();
    }

    private static ShoppingService CreateService(string dbName) =>
        new(DbContextFactory.Create(dbName));

    private static async Task<Guid> CreateList(ShoppingService svc, string name = "Test List")
    {
        var list = await svc.CreateListAsync(UserId, new CreateShoppingListDto(name));
        return list.Id;
    }

    [Fact]
    public async Task CreateList_AddsListToDb()
    {
        var dbName  = SeedBase();
        var service = CreateService(dbName);

        var list = await service.CreateListAsync(UserId, new CreateShoppingListDto("Mi lista"));

        Assert.Equal("Mi lista", list.Name);
    }

    [Fact]
    public async Task AddItem_AppendsProductToList()
    {
        var dbName  = SeedBase();
        var service = CreateService(dbName);
        var listId  = await CreateList(service);

        var result = await service.AddItemAsync(listId, UserId,
            new AddShoppingListItemDto(Product1, 2m));

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(Product1, result.Items[0].ProductId);
        Assert.Equal(2m, result.Items[0].Quantity);
    }

    [Fact]
    public async Task AddItem_IncrementsQuantity_WhenProductAlreadyInList()
    {
        var dbName  = SeedBase();
        var service = CreateService(dbName);
        var listId  = await CreateList(service);

        await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product1, 1m));
        var result = await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product1, 2m));

        Assert.Single(result!.Items);
        Assert.Equal(3m, result.Items[0].Quantity);
    }

    [Fact]
    public async Task ToggleItem_MarksItemPurchased_ThenUnpurchased()
    {
        var dbName  = SeedBase();
        var service = CreateService(dbName);
        var listId  = await CreateList(service);
        var list    = await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product1));
        var itemId  = list!.Items[0].Id;

        await service.MarkItemPurchasedAsync(listId, itemId, UserId);
        var after1 = await service.GetListAsync(listId, UserId);
        Assert.True(after1!.Items[0].IsPurchased);

        await service.MarkItemPurchasedAsync(listId, itemId, UserId);
        var after2 = await service.GetListAsync(listId, UserId);
        Assert.False(after2!.Items[0].IsPurchased);
    }

    [Fact]
    public async Task Optimize_AssignsItemsToCheapestStore()
    {
        var dbName  = SeedBase();
        AddPrice(dbName, Product1, StoreA, 900m);
        AddPrice(dbName, Product1, StoreB, 1200m);
        AddPrice(dbName, Product2, StoreA, 800m);
        AddPrice(dbName, Product2, StoreB, 1100m);

        var service = CreateService(dbName);
        var listId  = await CreateList(service);
        await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product1));
        await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product2));

        var result = await service.OptimizeAsync(listId, UserId);

        Assert.NotNull(result);
        Assert.Single(result.StoreGroups);
        Assert.Equal("MaxiPalí", result.StoreGroups[0].StoreName);
        Assert.Equal(1700m, result.TotalEstimatedCost);
    }

    [Fact]
    public async Task Optimize_CollapsesToSingleStore_WhenSavingsBelowThreshold()
    {
        // Product1 is slightly cheaper at StoreB, Product2 slightly cheaper at StoreA.
        // Splitting saves only 50 CRC total — well below the 2000 CRC travel threshold.
        // The optimizer should collapse back to a single store and report TotalSavings = 0.
        var dbName  = SeedBase(travelThreshold: 2000m);
        AddPrice(dbName, Product1, StoreA, 1000m);
        AddPrice(dbName, Product1, StoreB,  990m);  // 10 cheaper at B
        AddPrice(dbName, Product2, StoreA,  800m);  // 40 cheaper at A
        AddPrice(dbName, Product2, StoreB,  840m);

        var service = CreateService(dbName);
        var listId  = await CreateList(service);
        await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product1));
        await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product2));

        var result = await service.OptimizeAsync(listId, UserId);

        Assert.NotNull(result);
        Assert.Equal(0m, result.TotalSavings);
    }

    [Fact]
    public async Task Optimize_ReturnsEmptyGroups_WhenAllItemsPurchased()
    {
        var dbName  = SeedBase();
        AddPrice(dbName, Product1, StoreA, 900m);

        var service = CreateService(dbName);
        var listId  = await CreateList(service);
        var list    = await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product1));
        await service.MarkItemPurchasedAsync(listId, list!.Items[0].Id, UserId);

        var result = await service.OptimizeAsync(listId, UserId);

        Assert.NotNull(result);
        Assert.Empty(result.StoreGroups);
        Assert.Equal(0m, result.TotalEstimatedCost);
    }

    [Fact]
    public async Task DeleteList_RemovesListAndItems()
    {
        var dbName  = SeedBase();
        var service = CreateService(dbName);
        var listId  = await CreateList(service);
        await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product1));

        var ok = await service.DeleteListAsync(listId, UserId);

        Assert.True(ok);
        using var verify = DbContextFactory.Create(dbName);
        Assert.Empty(verify.ShoppingLists);
        Assert.Empty(verify.ShoppingListItems);
    }

    [Fact]
    public async Task DeleteList_ReturnsFalse_WhenListBelongsToDifferentUser()
    {
        var dbName  = SeedBase();
        var service = CreateService(dbName);
        var listId  = await CreateList(service);

        var ok = await service.DeleteListAsync(listId, Guid.NewGuid());

        Assert.False(ok);
    }

    [Fact]
    public async Task Optimize_RecommendsChainLevelPrice_WithNullStoreId()
    {
        var dbName = SeedBase();
        AddChainPrice(dbName, Product1, StoreChain.Walmart, 700m); // cheaper, chain-level scraped
        AddPrice(dbName, Product1, StoreA, 900m); // specific MaxiPalí location, user-submitted

        var service = CreateService(dbName);
        var listId  = await CreateList(service);
        await service.AddItemAsync(listId, UserId, new AddShoppingListItemDto(Product1));

        var result = await service.OptimizeAsync(listId, UserId);

        Assert.NotNull(result);
        var group = Assert.Single(result.StoreGroups);
        Assert.Null(group.StoreId);
        Assert.Equal("Walmart", group.StoreName);
        Assert.Equal(StoreChain.Walmart, group.Chain);
        Assert.Equal(700m, group.GroupTotal);
    }
}

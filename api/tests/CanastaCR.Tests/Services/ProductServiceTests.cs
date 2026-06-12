using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Core.Interfaces;
using CanastaCR.Tests.Helpers;
using Moq;

namespace CanastaCR.Tests.Services;

public class ProductServiceTests
{
    private ProductService CreateService(Mock<IOpenFoodFactsClient>? mockOff = null)
    {
        var off = mockOff ?? new Mock<IOpenFoodFactsClient>();
        return new ProductService(DbContextFactory.Create(), off.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsProduct_WhenExists()
    {
        var db = DbContextFactory.Create();
        var product = new Product { Id = Guid.NewGuid(), Barcode = "123", Name = "Apple", Brand = "Local", Category = "Fruit", CreatedAt = DateTimeOffset.UtcNow };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var result = await service.GetByIdAsync(product.Id);

        Assert.NotNull(result);
        Assert.Equal(product.Name, result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenProductDoesNotExist()
    {
        var service = CreateService();
        var result = await service.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByBarcodeAsync_ReturnsProduct_WhenBarcodeExists()
    {
        var db = DbContextFactory.Create();
        const string barcode = "5901234123457";
        var product = new Product { Id = Guid.NewGuid(), Barcode = barcode, Name = "Tomato Sauce", Brand = "Brand X", Category = "Condiments", CreatedAt = DateTimeOffset.UtcNow };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var result = await service.GetByBarcodeAsync(barcode);

        Assert.NotNull(result);
        Assert.Equal(barcode, result.Barcode);
    }

    [Fact]
    public async Task GetByBarcodeAsync_ReturnsNull_WhenBarcodeDoesNotExist()
    {
        var service = CreateService();
        var result = await service.GetByBarcodeAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Top50Products_OrderedByName()
    {
        var db = DbContextFactory.Create();
        for (int i = 0; i < 60; i++)
        {
            db.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Product {i:D2}",
                Brand = "Brand",
                Category = "Category",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();

        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var result = await service.GetAllAsync();

        Assert.Equal(50, result.Count);
        Assert.Equal("Product 00", result[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_IncludesLowestPrice_FromValidPriceReports()
    {
        var db = DbContextFactory.Create();
        var product = new Product { Id = Guid.NewGuid(), Name = "Milk", Brand = "Brand", Category = "Dairy", CreatedAt = DateTimeOffset.UtcNow };
        var store1 = new Store { Id = Guid.NewGuid(), Name = "Store1", Chain = StoreChain.AutoMercado, City = "SJ", Lat = 0, Lng = 0 };
        var store2 = new Store { Id = Guid.NewGuid(), Name = "Store2", Chain = StoreChain.MasXMenos, City = "SJ", Lat = 0, Lng = 0 };

        product.PriceReports =
        [
            new PriceReport { Price = 2000, StoreId = store1.Id, Store = store1, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Source = PriceSource.UserSubmitted },
            new PriceReport { Price = 1800, StoreId = store2.Id, Store = store2, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Source = PriceSource.UserSubmitted }
        ];

        db.Products.Add(product);
        db.Stores.Add(store1);
        db.Stores.Add(store2);
        await db.SaveChangesAsync();

        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var result = await service.GetAllAsync();

        Assert.Single(result);
        Assert.Equal(1800, result[0].LowestPrice);
        Assert.Equal("Store2", result[0].LowestPriceStore);
    }

    [Fact]
    public async Task GetAllAsync_ExcludesExpiredPrices()
    {
        var db = DbContextFactory.Create();
        var product = new Product { Id = Guid.NewGuid(), Name = "Bread", Brand = "Bakery", Category = "Bakery", CreatedAt = DateTimeOffset.UtcNow };
        var store = new Store { Id = Guid.NewGuid(), Name = "Store", Chain = StoreChain.AutoMercado, City = "SJ", Lat = 0, Lng = 0 };
        product.PriceReports =
        [
            new PriceReport { Price = 2000, StoreId = store.Id, Store = store, ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), Source = PriceSource.UserSubmitted },
        ];

        db.Products.Add(product);
        db.Stores.Add(store);
        await db.SaveChangesAsync();

        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var result = await service.GetAllAsync();

        Assert.Single(result);
        Assert.Null(result[0].LowestPrice);
    }

    [Fact]
    public async Task SearchAsync_FiltersProductsByName()
    {
        var db = DbContextFactory.Create();
        db.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Apple Juice", Brand = "Brand", Category = "Beverages", CreatedAt = DateTimeOffset.UtcNow });
        db.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Orange Juice", Brand = "Brand", Category = "Beverages", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var result = await service.SearchAsync("apple");

        Assert.Single(result);
        Assert.Equal("Apple Juice", result[0].Name);
    }

    [Fact]
    public async Task SearchAsync_FiltersByBrand_WhenBrandMatches()
    {
        var db = DbContextFactory.Create();
        db.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Product A", Brand = "TargetBrand", Category = "Category", CreatedAt = DateTimeOffset.UtcNow });
        db.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Product B", Brand = "OtherBrand", Category = "Category", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var result = await service.SearchAsync("targetbrand");

        Assert.Single(result);
        Assert.Equal("TargetBrand", result[0].Brand);
    }

    [Fact]
    public async Task SearchAsync_IsCaseInsensitive()
    {
        var db = DbContextFactory.Create();
        db.Products.Add(new Product { Id = Guid.NewGuid(), Name = "MILK", Brand = "Brand", Category = "Dairy", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var result = await service.SearchAsync("milk");

        Assert.Single(result);
    }

    [Fact]
    public async Task SearchAsync_ReturnsTop20Results()
    {
        var db = DbContextFactory.Create();
        for (int i = 0; i < 30; i++)
        {
            db.Products.Add(new Product { Id = Guid.NewGuid(), Name = $"Apple {i}", Brand = "Brand", Category = "Fruit", CreatedAt = DateTimeOffset.UtcNow });
        }
        await db.SaveChangesAsync();

        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var result = await service.SearchAsync("apple");

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public async Task LookupOrCreateByBarcodeAsync_ReturnsExisting_WhenProductExists()
    {
        var db = DbContextFactory.Create();
        const string barcode = "5901234123457";
        var product = new Product { Id = Guid.NewGuid(), Barcode = barcode, Name = "Existing", Brand = "Brand", Category = "Category", CreatedAt = DateTimeOffset.UtcNow };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var mockOff = new Mock<IOpenFoodFactsClient>();
        var service = new ProductService(db, mockOff.Object);
        var result = await service.LookupOrCreateByBarcodeAsync(barcode);

        Assert.Equal("Existing", result.Name);
        mockOff.Verify(x => x.LookupByBarcodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LookupOrCreateByBarcodeAsync_CreatesFromOpenFoodFacts_WhenNotFound()
    {
        var db = DbContextFactory.Create();
        const string barcode = "5901234123457";

        var offData = new CreateProductDto(Barcode: barcode, Name: "Tomato Sauce", Brand: "DelMonte", Category: "Condiments", ImageUrl: "https://example.com/image.jpg", Description: null);
        var mockOff = new Mock<IOpenFoodFactsClient>();
        mockOff.Setup(x => x.LookupByBarcodeAsync(barcode, It.IsAny<CancellationToken>())).ReturnsAsync(offData);

        var service = new ProductService(db, mockOff.Object);
        var result = await service.LookupOrCreateByBarcodeAsync(barcode);

        Assert.Equal("Tomato Sauce", result.Name);
        Assert.Equal("DelMonte", result.Brand);
        Assert.Equal(barcode, result.Barcode);

        var saved = await db.Products.FindAsync(result.Id);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task LookupOrCreateByBarcodeAsync_UsesDefaultName_WhenOpenFoodFactsReturnsNull()
    {
        var db = DbContextFactory.Create();
        const string barcode = "unknownbarcode";

        var mockOff = new Mock<IOpenFoodFactsClient>();
        mockOff.Setup(x => x.LookupByBarcodeAsync(barcode, It.IsAny<CancellationToken>())).ReturnsAsync((CreateProductDto?)null);

        var service = new ProductService(db, mockOff.Object);
        var result = await service.LookupOrCreateByBarcodeAsync(barcode);

        Assert.Equal("Unknown product", result.Name);
        Assert.Equal(barcode, result.Barcode);
    }

    [Fact]
    public async Task CreateAsync_CreatesNewProduct_WithAllFields()
    {
        var service = CreateService();

        var dto = new CreateProductDto(
            Barcode: "newbarcode",
            Name: "New Product",
            Brand: "NewBrand",
            Category: "NewCategory",
            ImageUrl: "https://example.com/new.jpg",
            Description: "A new product"
        );

        var result = await service.CreateAsync(dto);

        Assert.Equal("New Product", result.Name);
        Assert.Equal("NewBrand", result.Brand);
        Assert.Equal("newbarcode", result.Barcode);
        Assert.Equal("A new product", result.Description);
    }

    [Fact]
    public async Task CreateAsync_PersistsToDatabase()
    {
        var db = DbContextFactory.Create();
        var service = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);

        var dto = new CreateProductDto(Barcode: "persist", Name: "Persist Test", Brand: "Brand", Category: "Cat", ImageUrl: null, Description: null);
        var result = await service.CreateAsync(dto);

        var saved = await db.Products.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal("Persist Test", saved.Name);
    }
}

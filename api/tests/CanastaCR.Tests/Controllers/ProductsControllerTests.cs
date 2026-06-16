using CanastaCR.Api.Controllers;
using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Core.Interfaces;
using CanastaCR.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CanastaCR.Tests.Controllers;

public class ProductsControllerTests
{
    private ProductsController CreateController(ProductService? productService = null, PriceService? priceService = null)
    {
        productService ??= new ProductService(DbContextFactory.Create(), new Mock<IOpenFoodFactsClient>().Object);
        priceService ??= new PriceService(DbContextFactory.Create());
        return new ProductsController(productService, priceService);
    }

    [Fact]
    public async Task GetAll_ReturnsOkResult_WithProductList()
    {
        var controller = CreateController();
        var result = await controller.GetAll(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.IsAssignableFrom<List<ProductSearchResultDto>>(okResult.Value);
    }

    [Fact]
    public async Task GetById_ReturnsOkResult_WhenProductExists()
    {
        var db = DbContextFactory.Create();
        var productId = Guid.NewGuid();
        db.Products.Add(new() { Id = productId, Name = "Test Product", Brand = "Brand", Category = "Cat", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var productService = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var controller = new ProductsController(productService, new PriceService(db));

        var result = await controller.GetById(productId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var dto = Assert.IsType<ProductDto>(okResult.Value);
        Assert.Equal("Test Product", dto.Name);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenProductDoesNotExist()
    {
        var controller = CreateController();
        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetByBarcode_ReturnsOkResult_WhenBarcodeExists()
    {
        var db = DbContextFactory.Create();
        const string barcode = "5901234123457";
        db.Products.Add(new() { Id = Guid.NewGuid(), Barcode = barcode, Name = "Barcode Product", Brand = "B", Category = "C", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var productService = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var controller = new ProductsController(productService, new PriceService(db));

        var result = await controller.GetByBarcode(barcode, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ProductDto>(okResult.Value);
        Assert.Equal(barcode, dto.Barcode);
    }

    [Fact]
    public async Task GetByBarcode_CreatesFromOpenFoodFacts_WhenNotInDb()
    {
        const string barcode = "unknownbarcode";
        var offData = new CreateProductDto(Barcode: barcode, Name: "From OFF", Brand: "Brand", Category: "Cat", ImageUrl: null, Description: null);

        var mockOff = new Mock<IOpenFoodFactsClient>();
        mockOff.Setup(x => x.LookupByBarcodeAsync(barcode, It.IsAny<CancellationToken>())).ReturnsAsync(offData);

        var db = DbContextFactory.Create();
        var productService = new ProductService(db, mockOff.Object);
        var controller = new ProductsController(productService, new PriceService(db));

        var result = await controller.GetByBarcode(barcode, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ProductDto>(okResult.Value);
        Assert.Equal("From OFF", dto.Name);
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenQueryIsEmpty()
    {
        var controller = CreateController();
        var result = await controller.Search(string.Empty, CancellationToken.None);

        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badResult.StatusCode);
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenQueryIsNull()
    {
        var controller = CreateController();
        var result = await controller.Search(null!, CancellationToken.None);

        var badResult = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Search_ReturnsOkResult_WithMatchingProducts()
    {
        var db = DbContextFactory.Create();
        db.Products.Add(new() { Id = Guid.NewGuid(), Name = "Apple Juice", Brand = "B", Category = "C", CreatedAt = DateTimeOffset.UtcNow });
        db.Products.Add(new() { Id = Guid.NewGuid(), Name = "Orange Juice", Brand = "B", Category = "C", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var productService = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var controller = new ProductsController(productService, new PriceService(db));

        var result = await controller.Search("apple", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<List<ProductSearchResultDto>>(okResult.Value);
        Assert.Single(dto);
        Assert.Equal("Apple Juice", dto[0].Name);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenAuthorized()
    {
        var controller = CreateController();
        var dto = new CreateProductDto(Barcode: "newbar", Name: "New", Brand: "B", Category: "C", ImageUrl: null, Description: null);

        var result = await controller.Create(dto, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal(nameof(controller.GetById), createdResult.ActionName);
        var productDto = Assert.IsType<ProductDto>(createdResult.Value);
        Assert.Equal("New", productDto.Name);
    }

    [Fact]
    public async Task Update_ReturnsOkResult_WithUpdatedProduct()
    {
        var db = DbContextFactory.Create();
        var productId = Guid.NewGuid();
        db.Products.Add(new() { Id = productId, Name = ProductService.UnnamedProductPlaceholder, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var productService = new ProductService(db, new Mock<IOpenFoodFactsClient>().Object);
        var controller = new ProductsController(productService, new PriceService(db));

        var result = await controller.Update(productId, new UpdateProductDto("Galletas María"), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ProductDto>(okResult.Value);
        Assert.Equal("Galletas María", dto.Name);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenProductDoesNotExist()
    {
        var controller = CreateController();
        var result = await controller.Update(Guid.NewGuid(), new UpdateProductDto("Nombre"), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPrices_ReturnsOkResult_WhenComparisonsExist()
    {
        var db = DbContextFactory.Create();
        var productId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var store = new Store { Id = storeId, Name = "Store", Chain = StoreChain.AutoMercado, City = "SJ", Lat = 0, Lng = 0 };
        var product = new Product { Id = productId, Name = "Prod", Brand = "B", Category = "C", CreatedAt = DateTimeOffset.UtcNow };
        product.PriceReports =
        [
            new PriceReport { Price = 2000, StoreId = storeId, Store = store, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Source = PriceSource.UserSubmitted }
        ];

        db.Products.Add(product);
        db.Stores.Add(store);
        await db.SaveChangesAsync();

        var priceService = new PriceService(db);
        var controller = new ProductsController(new ProductService(db, new Mock<IOpenFoodFactsClient>().Object), priceService);

        var result = await controller.GetPrices(productId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetPrices_ReturnsNotFound_WhenProductDoesNotExist()
    {
        var controller = CreateController();
        var result = await controller.GetPrices(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}

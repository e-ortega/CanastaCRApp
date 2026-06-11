using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanastaCR.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(ProductService productService, PriceService priceService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var results = await productService.GetAllAsync(ct);
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var product = await productService.GetByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetByBarcode(string barcode, CancellationToken ct)
    {
        var product = await productService.GetByBarcodeAsync(barcode, ct);
        if (product is not null) return Ok(product);

        // Auto-lookup from Open Food Facts
        var created = await productService.LookupOrCreateByBarcodeAsync(barcode, ct);
        return Ok(created);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest("Query is required.");
        var results = await productService.SearchAsync(q, ct);
        return Ok(results);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateProductDto dto, CancellationToken ct)
    {
        var product = await productService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpGet("{id:guid}/prices")]
    public async Task<IActionResult> GetPrices(Guid id, CancellationToken ct)
    {
        var comparison = await priceService.GetComparisonAsync(id, ct);
        return comparison is null ? NotFound() : Ok(comparison);
    }
}

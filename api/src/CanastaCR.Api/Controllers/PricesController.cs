using System.Security.Claims;
using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanastaCR.Api.Controllers;

[ApiController]
[Route("api/prices")]
public class PricesController(PriceService priceService) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Report(CreatePriceReportDto dto, CancellationToken ct)
    {
        var userId = GetUserId();
        var report = await priceService.ReportAsync(dto, userId, ct);
        return Ok(report);
    }

    [HttpGet("savings")]
    public async Task<IActionResult> GetSavings(CancellationToken ct)
    {
        var summary = await priceService.GetSavingsSummaryAsync(ct);
        return Ok(summary);
    }

    [HttpGet("store/{storeId:guid}")]
    public async Task<IActionResult> GetForStore(
        Guid storeId, [FromQuery] int page = 0, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var prices = await priceService.GetRecentForStoreAsync(storeId, page, pageSize, ct);
        return Ok(prices);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null ? Guid.Parse(claim) : null;
    }
}

using System.Security.Claims;
using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanastaCR.Api.Controllers;

[ApiController]
[Route("api/pantry")]
[Authorize]
public class PantryController(PantryService pantryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var items = await pantryService.GetItemsAsync(GetUserId(), ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(UpsertPantryItemDto dto, CancellationToken ct)
    {
        var item = await pantryService.UpsertAsync(GetUserId(), dto, ct);
        return item is null ? NotFound("Product not found.") : Ok(item);
    }

    [HttpPatch("{id:guid}/quantity")]
    public async Task<IActionResult> UpdateQuantity(Guid id, [FromBody] decimal quantity, CancellationToken ct)
    {
        var ok = await pantryService.UpdateQuantityAsync(GetUserId(), id, quantity, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await pantryService.DeleteAsync(GetUserId(), id, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("add-low-stock-to-list/{listId:guid}")]
    public async Task<IActionResult> AddLowStockToList(Guid listId, CancellationToken ct)
    {
        var count = await pantryService.AddLowStockToListAsync(GetUserId(), listId, ct);
        return Ok(new { added = count });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

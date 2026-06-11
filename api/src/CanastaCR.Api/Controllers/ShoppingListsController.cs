using System.Security.Claims;
using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanastaCR.Api.Controllers;

[ApiController]
[Route("api/shopping-lists")]
[Authorize]
public class ShoppingListsController(ShoppingService shoppingService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var lists = await shoppingService.GetListsAsync(GetUserId(), ct);
        return Ok(lists);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var list = await shoppingService.GetListAsync(id, GetUserId(), ct);
        return list is null ? NotFound() : Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateShoppingListDto dto, CancellationToken ct)
    {
        var list = await shoppingService.CreateListAsync(GetUserId(), dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = list.Id }, list);
    }

    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, AddShoppingListItemDto dto, CancellationToken ct)
    {
        var list = await shoppingService.AddItemAsync(id, GetUserId(), dto, ct);
        return list is null ? NotFound() : Ok(list);
    }

    [HttpPatch("{id:guid}/items/{itemId:guid}/toggle")]
    public async Task<IActionResult> ToggleItem(Guid id, Guid itemId, CancellationToken ct)
    {
        var ok = await shoppingService.MarkItemPurchasedAsync(id, itemId, GetUserId(), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await shoppingService.DeleteListAsync(id, GetUserId(), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/optimize")]
    public async Task<IActionResult> Optimize(Guid id, CancellationToken ct)
    {
        var result = await shoppingService.OptimizeAsync(id, GetUserId(), ct);
        return result is null ? NotFound() : Ok(result);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

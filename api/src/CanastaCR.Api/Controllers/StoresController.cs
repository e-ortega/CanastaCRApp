using CanastaCR.Core.DTOs;
using CanastaCR.Core.Enums;
using CanastaCR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Api.Controllers;

[ApiController]
[Route("api/stores")]
public class StoresController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] StoreChain? chain, CancellationToken ct)
    {
        var query = db.Stores.AsQueryable();
        if (chain.HasValue) query = query.Where(s => s.Chain == chain.Value);

        var stores = await query
            .OrderBy(s => s.Chain).ThenBy(s => s.Name)
            .Select(s => new StoreDto(s.Id, s.Name, s.Chain, s.Address, s.City, s.Lat, s.Lng))
            .ToListAsync(ct);

        return Ok(stores);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var store = await db.Stores.FindAsync([id], ct);
        if (store is null) return NotFound();
        return Ok(new StoreDto(store.Id, store.Name, store.Chain, store.Address, store.City, store.Lat, store.Lng));
    }
}

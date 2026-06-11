using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Api.Services;

public class ShoppingService(AppDbContext db)
{
    public async Task<List<ShoppingListDto>> GetListsAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.ShoppingLists
            .Include(l => l.Items).ThenInclude(i => i.Product)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => MapListToDto(l))
            .ToListAsync(ct);
    }

    public async Task<ShoppingListDto?> GetListAsync(Guid listId, Guid userId, CancellationToken ct = default)
    {
        var list = await db.ShoppingLists
            .Include(l => l.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId, ct);
        return list is null ? null : MapListToDto(list);
    }

    public async Task<ShoppingListDto> CreateListAsync(Guid userId, CreateShoppingListDto dto, CancellationToken ct = default)
    {
        var list = new ShoppingList
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync(ct);
        return MapListToDto(list);
    }

    public async Task<bool> DeleteListAsync(Guid listId, Guid userId, CancellationToken ct = default)
    {
        var list = await db.ShoppingLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId, ct);
        if (list is null) return false;
        db.ShoppingListItems.RemoveRange(list.Items);
        db.ShoppingLists.Remove(list);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ShoppingListDto?> AddItemAsync(
        Guid listId, Guid userId, AddShoppingListItemDto dto, CancellationToken ct = default)
    {
        if (!await db.ShoppingLists.AnyAsync(l => l.Id == listId && l.UserId == userId, ct))
            return null;

        var existing = await db.ShoppingListItems
            .FirstOrDefaultAsync(i => i.ShoppingListId == listId && i.ProductId == dto.ProductId, ct);

        if (existing is not null)
        {
            existing.Quantity += dto.Quantity;
        }
        else
        {
            if (!await db.Products.AnyAsync(p => p.Id == dto.ProductId, ct))
                return null;

            db.ShoppingListItems.Add(new ShoppingListItem
            {
                Id             = Guid.NewGuid(),
                ShoppingListId = listId,
                ProductId      = dto.ProductId,
                Quantity       = dto.Quantity,
                Unit           = dto.Unit
            });
        }

        await db.SaveChangesAsync(ct);

        var updated = await db.ShoppingLists
            .Include(l => l.Items).ThenInclude(i => i.Product)
            .FirstAsync(l => l.Id == listId, ct);
        return MapListToDto(updated);
    }

    public async Task<bool> MarkItemPurchasedAsync(
        Guid listId, Guid itemId, Guid userId, CancellationToken ct = default)
    {
        var item = await db.ShoppingListItems
            .Include(i => i.ShoppingList)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.ShoppingListId == listId && i.ShoppingList.UserId == userId, ct);
        if (item is null) return false;

        item.IsPurchased = !item.IsPurchased;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ShoppingOptimizationResultDto?> OptimizeAsync(
        Guid listId, Guid userId, CancellationToken ct = default)
    {
        var list = await db.ShoppingLists
            .Include(l => l.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId, ct);
        if (list is null) return null;

        var prefs = await db.UserPreferences.FindAsync([userId], ct)
            ?? new UserPreferences { TravelCostThreshold = 2000m, MaxStoresPerTrip = 2 };

        var activeItems = list.Items.Where(i => !i.IsPurchased).ToList();
        if (activeItems.Count == 0)
            return new ShoppingOptimizationResultDto(listId, 0, 0, 0, 0, []);

        var now = DateTimeOffset.UtcNow;
        var productIds = activeItems.Select(i => i.ProductId).ToList();

        var latestPrices = await db.PriceReports
            .Include(r => r.Store)
            .Where(r => productIds.Contains(r.ProductId) && r.ExpiresAt > now)
            .GroupBy(r => new { r.ProductId, r.StoreId })
            .Select(g => g.OrderByDescending(r => r.ReportedAt).First())
            .ToListAsync(ct);

        // Build price matrix: productId -> list of (store, price)
        var priceMatrix = latestPrices
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Greedy optimizer: assign each item to its cheapest store
        var assignments = new Dictionary<Guid, (PriceReport best, decimal highest)>();
        foreach (var item in activeItems)
        {
            if (!priceMatrix.TryGetValue(item.ProductId, out var prices) || prices.Count == 0)
                continue;

            var sorted = prices.OrderBy(p => p.Price).ToList();
            assignments[item.ProductId] = (sorted[0], sorted[^1].Price);
        }

        // Group by store
        var storeGroups = assignments
            .GroupBy(kvp => kvp.Value.best.StoreId)
            .Take(prefs.MaxStoresPerTrip)
            .Select(g =>
            {
                var storeReport = g.First().Value.best;
                var items = g.Select(kvp =>
                {
                    var listItem = activeItems.First(i => i.ProductId == kvp.Key);
                    var price = kvp.Value.best.Price;
                    var qty = listItem.Quantity;
                    return new OptimizedItemDto(
                        kvp.Key,
                        kvp.Value.best.Product.Name,
                        price,
                        qty,
                        listItem.Unit,
                        price * qty,
                        (kvp.Value.highest - price) * qty);
                }).ToList();

                return new StoreShoppingGroupDto(
                    g.Key,
                    storeReport.Store.Name,
                    storeReport.Store.Chain,
                    items.Sum(i => i.LineTotal),
                    items);
            })
            .ToList();

        var totalCost = storeGroups.Sum(g => g.GroupTotal);

        // Calculate what it would cost at the single most expensive store
        var singleStoreCost = activeItems
            .Sum(item => priceMatrix.TryGetValue(item.ProductId, out var prices) && prices.Count > 0
                ? prices.Max(p => p.Price) * item.Quantity
                : 0);

        var savings = singleStoreCost - totalCost;
        var savingsPct = singleStoreCost > 0 ? savings / singleStoreCost * 100 : 0;

        // Only recommend split if savings exceed threshold
        if (storeGroups.Count > 1 && savings < prefs.TravelCostThreshold)
        {
            // Collapse to single best store
            var bestStore = storeGroups.OrderByDescending(g => g.GroupTotal).Last();
            storeGroups = [bestStore with { Items = storeGroups.SelectMany(g => g.Items).ToList() }];
            totalCost = singleStoreCost;
            savings = 0;
            savingsPct = 0;
        }

        return new ShoppingOptimizationResultDto(
            listId, totalCost, singleStoreCost, savings, savingsPct, storeGroups);
    }

    private static ShoppingListDto MapListToDto(ShoppingList l) =>
        new(l.Id, l.Name, l.CreatedAt,
            l.Items.Select(i => new ShoppingListItemDto(
                i.Id, i.ProductId, i.Product?.Name ?? "",
                i.Product?.ImageUrl, i.Quantity, i.Unit, i.IsPurchased))
            .ToList());
}

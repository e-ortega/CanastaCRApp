using System.Security.Claims;
using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Api.Services;

public class PantryService(AppDbContext db)
{
    public async Task<List<PantryItemDto>> GetItemsAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.PantryItems
            .Where(i => i.UserId == userId)
            .Include(i => i.Product)
            .OrderBy(i => i.Product.Name)
            .Select(i => MapToDto(i))
            .ToListAsync(ct);
    }

    public async Task<PantryItemDto?> UpsertAsync(Guid userId, UpsertPantryItemDto dto, CancellationToken ct = default)
    {
        var existing = await db.PantryItems
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.UserId == userId && i.ProductId == dto.ProductId, ct);

        if (existing is not null)
        {
            existing.Quantity = dto.Quantity;
            existing.Unit = dto.Unit;
            existing.MinThreshold = dto.MinThreshold;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return MapToDto(existing);
        }

        var product = await db.Products.FindAsync([dto.ProductId], ct);
        if (product is null) return null;

        var item = new PantryItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            Unit = dto.Unit,
            MinThreshold = dto.MinThreshold,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        item.Product = product;
        db.PantryItems.Add(item);
        await db.SaveChangesAsync(ct);
        return MapToDto(item);
    }

    public async Task<bool> UpdateQuantityAsync(Guid userId, Guid itemId, decimal quantity, CancellationToken ct = default)
    {
        var item = await db.PantryItems.FirstOrDefaultAsync(i => i.Id == itemId && i.UserId == userId, ct);
        if (item is null) return false;
        item.Quantity = quantity;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid itemId, CancellationToken ct = default)
    {
        var item = await db.PantryItems.FirstOrDefaultAsync(i => i.Id == itemId && i.UserId == userId, ct);
        if (item is null) return false;
        db.PantryItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // Returns ids of items added to the specified shopping list
    public async Task<int> AddLowStockToListAsync(Guid userId, Guid listId, CancellationToken ct = default)
    {
        var list = await db.ShoppingLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId, ct);
        if (list is null) return 0;

        var lowStock = await db.PantryItems
            .Where(i => i.UserId == userId && i.Quantity < i.MinThreshold)
            .ToListAsync(ct);

        var existingProductIds = list.Items.Select(i => i.ProductId).ToHashSet();
        var added = 0;

        foreach (var pantryItem in lowStock)
        {
            if (existingProductIds.Contains(pantryItem.ProductId)) continue;
            db.ShoppingListItems.Add(new ShoppingListItem
            {
                Id = Guid.NewGuid(),
                ShoppingListId = listId,
                ProductId = pantryItem.ProductId,
                Quantity = pantryItem.MinThreshold - pantryItem.Quantity,
                Unit = pantryItem.Unit,
                IsPurchased = false,
            });
            added++;
        }

        if (added > 0) await db.SaveChangesAsync(ct);
        return added;
    }

    private static PantryItemDto MapToDto(PantryItem i) => new(
        i.Id,
        i.ProductId,
        i.Product.Name,
        i.Product.ImageUrl,
        i.Quantity,
        i.Unit,
        i.MinThreshold,
        i.Quantity < i.MinThreshold,
        i.LastPurchasedAt
    );
}

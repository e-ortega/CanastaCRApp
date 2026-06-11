using CanastaCR.Core.Enums;

namespace CanastaCR.Core.Entities;

public class ShoppingListItem
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public QuantityUnit Unit { get; set; } = QuantityUnit.Unit;
    public bool IsPurchased { get; set; }

    public ShoppingList ShoppingList { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

using CanastaCR.Core.Enums;

namespace CanastaCR.Core.Entities;

public class PantryItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public QuantityUnit Unit { get; set; } = QuantityUnit.Unit;
    public decimal MinThreshold { get; set; }
    public DateTimeOffset? LastPurchasedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

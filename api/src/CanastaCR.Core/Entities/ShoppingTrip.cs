namespace CanastaCR.Core.Entities;

public class ShoppingTrip
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid StoreId { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal EstimatedSavings { get; set; }
    public DateTimeOffset Date { get; set; }

    public User User { get; set; } = null!;
    public Store Store { get; set; } = null!;
    public ICollection<ShoppingTripItem> Items { get; set; } = [];
}

namespace CanastaCR.Core.Entities;

public class ShoppingTripItem
{
    public Guid Id { get; set; }
    public Guid ShoppingTripId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }

    public ShoppingTrip ShoppingTrip { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

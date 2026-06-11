namespace CanastaCR.Core.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PriceReport> PriceReports { get; set; } = [];
    public ICollection<ShoppingListItem> ShoppingListItems { get; set; } = [];
    public ICollection<PantryItem> PantryItems { get; set; } = [];
}

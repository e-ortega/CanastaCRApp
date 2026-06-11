namespace CanastaCR.Core.Entities;

public class ShoppingList
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<ShoppingListItem> Items { get; set; } = [];
}

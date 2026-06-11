namespace CanastaCR.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int ReputationPoints { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public UserPreferences? Preferences { get; set; }
    public ICollection<PriceReport> PriceReports { get; set; } = [];
    public ICollection<ShoppingList> ShoppingLists { get; set; } = [];
    public ICollection<PantryItem> PantryItems { get; set; } = [];
    public ICollection<ShoppingTrip> ShoppingTrips { get; set; } = [];
}

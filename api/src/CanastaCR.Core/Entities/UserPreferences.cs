namespace CanastaCR.Core.Entities;

public class UserPreferences
{
    public Guid UserId { get; set; }
    public decimal TravelCostThreshold { get; set; } = 2000m;
    public int MaxStoresPerTrip { get; set; } = 2;
    public string Currency { get; set; } = "CRC";
    public double? HomeLat { get; set; }
    public double? HomeLng { get; set; }

    public User User { get; set; } = null!;
}

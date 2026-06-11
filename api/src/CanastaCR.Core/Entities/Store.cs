using CanastaCR.Core.Enums;

namespace CanastaCR.Core.Entities;

public class Store
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public StoreChain Chain { get; set; }
    public string? Address { get; set; }
    public string City { get; set; } = string.Empty;
    public double? Lat { get; set; }
    public double? Lng { get; set; }

    public ICollection<PriceReport> PriceReports { get; set; } = [];
    public ICollection<ShoppingTrip> ShoppingTrips { get; set; } = [];
}

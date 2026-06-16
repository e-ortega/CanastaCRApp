using CanastaCR.Core.Enums;

namespace CanastaCR.Core.Entities;

public class PriceReport
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }

    // Exactly one of (StoreId, Chain) is set, never both, never neither:
    // - UserSubmitted reports: StoreId set (a shopper observed this at a specific location),
    //   Chain left null — derive via Store.Chain when needed.
    // - Scraped reports: StoreId null, Chain set. Chains scraped so far (VTEX-based stores,
    //   PriceSmart) set one nationwide price, not a per-location price — see
    //   docs/ARCHITECTURE.md section 11. Writing the same price to every physical location of
    //   a chain would just be duplicate data for something that never varies by location.
    public Guid? StoreId { get; set; }
    public StoreChain? Chain { get; set; }

    public decimal Price { get; set; }
    public string Currency { get; set; } = "CRC";
    public PriceSource Source { get; set; }
    public Guid? ReportedById { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public Product Product { get; set; } = null!;
    public Store? Store { get; set; }
    public User? ReportedBy { get; set; }
}

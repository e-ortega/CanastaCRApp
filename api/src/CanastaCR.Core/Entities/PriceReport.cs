using CanastaCR.Core.Enums;

namespace CanastaCR.Core.Entities;

public class PriceReport
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "CRC";
    public PriceSource Source { get; set; }
    public Guid? ReportedById { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public Product Product { get; set; } = null!;
    public Store Store { get; set; } = null!;
    public User? ReportedBy { get; set; }
}

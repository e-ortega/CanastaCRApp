using CanastaCR.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Scraper.Persistence;

public class ScraperDbContext(DbContextOptions<ScraperDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<PriceReport> PriceReports => Set<PriceReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Barcode)
            .IsUnique()
            .HasFilter("\"Barcode\" IS NOT NULL");

        modelBuilder.Entity<PriceReport>()
            .Property(p => p.Price)
            .HasPrecision(12, 2);

        modelBuilder.Entity<PriceReport>()
            .HasIndex(p => new { p.ProductId, p.StoreId, p.ReportedAt });

        // Ignore navigation properties that belong to the main app context
        modelBuilder.Entity<Product>()
            .Ignore(p => p.ShoppingListItems)
            .Ignore(p => p.PantryItems);

        modelBuilder.Entity<Store>()
            .Ignore(s => s.ShoppingTrips);

        modelBuilder.Entity<PriceReport>()
            .Ignore(r => r.ReportedBy);
    }
}

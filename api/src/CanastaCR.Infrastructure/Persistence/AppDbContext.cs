using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<PriceReport> PriceReports => Set<PriceReport>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<ShoppingTrip> ShoppingTrips => Set<ShoppingTrip>();
    public DbSet<ShoppingTripItem> ShoppingTripItems => Set<ShoppingTripItem>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserPreferences>()
            .HasKey(p => p.UserId);

        modelBuilder.Entity<UserPreferences>()
            .HasOne(p => p.User)
            .WithOne(u => u.Preferences)
            .HasForeignKey<UserPreferences>(p => p.UserId);

        modelBuilder.Entity<PriceReport>()
            .HasIndex(p => new { p.ProductId, p.StoreId, p.ReportedAt });

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Barcode)
            .IsUnique()
            .HasFilter("\"Barcode\" IS NOT NULL");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<PriceReport>()
            .Property(p => p.Price)
            .HasPrecision(12, 2);

        modelBuilder.Entity<ShoppingListItem>()
            .Property(i => i.Quantity)
            .HasPrecision(10, 3);

        modelBuilder.Entity<PantryItem>()
            .Property(i => i.Quantity)
            .HasPrecision(10, 3);

        modelBuilder.Entity<PantryItem>()
            .Property(i => i.MinThreshold)
            .HasPrecision(10, 3);

        modelBuilder.Entity<UserPreferences>()
            .Property(p => p.TravelCostThreshold)
            .HasPrecision(12, 2);

        SeedStores(modelBuilder);
    }

    private static void SeedStores(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Store>().HasData(
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000001"), Name = "AutoMercado Escazú", Chain = StoreChain.AutoMercado, City = "Escazú", Address = "Multiplaza Escazú", Lat = 9.9196, Lng = -84.1349 },
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000002"), Name = "AutoMercado San Pedro", Chain = StoreChain.AutoMercado, City = "San José", Address = "Av. Central, San Pedro", Lat = 9.9348, Lng = -84.0489 },
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000003"), Name = "Más x Menos La Uruca", Chain = StoreChain.MasXMenos, City = "San José", Address = "La Uruca", Lat = 9.9608, Lng = -84.1013 },
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000004"), Name = "Más x Menos Cartago", Chain = StoreChain.MasXMenos, City = "Cartago", Address = "Centro Cartago", Lat = 9.8632, Lng = -83.9198 },
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000005"), Name = "MaxiPalí Alajuela", Chain = StoreChain.MaxiPali, City = "Alajuela", Address = "Alajuela Centro", Lat = 10.0162, Lng = -84.2116 },
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000006"), Name = "MaxiPalí Heredia", Chain = StoreChain.MaxiPali, City = "Heredia", Address = "Heredia Centro", Lat = 9.9985, Lng = -84.1171 },
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000007"), Name = "MegaSuper Tibás", Chain = StoreChain.MegaSuper, City = "San José", Address = "Tibás", Lat = 9.9742, Lng = -84.0821 },
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000008"), Name = "MegaSuper Desamparados", Chain = StoreChain.MegaSuper, City = "San José", Address = "Desamparados", Lat = 9.9031, Lng = -84.0766 },
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000009"), Name = "PriceSmart San José", Chain = StoreChain.PriceSmart, City = "San José", Address = "La Uruca", Lat = 9.9571, Lng = -84.1107 },
            new Store { Id = new Guid("11111111-0000-0000-0000-000000000010"), Name = "PriceSmart Alajuela", Chain = StoreChain.PriceSmart, City = "Alajuela", Address = "Alajuela", Lat = 10.0198, Lng = -84.2089 }
        );
    }
}

using System.Text;
using Microsoft.EntityFrameworkCore;
using CanastaCR.Api.Services;
using CanastaCR.Core.Interfaces;
using CanastaCR.Infrastructure.ExternalServices;
using CanastaCR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CanastaCR API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<IOpenFoodFactsClient, OpenFoodFactsClient>(c =>
{
    c.BaseAddress = new Uri("https://world.openfoodfacts.org/");
    c.DefaultRequestHeaders.Add("User-Agent", "CanastaCR/1.0 (price-comparison-app)");
});

builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<PriceService>();
builder.Services.AddScoped<ShoppingService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PantryService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
    // Dev-only fixtures (admin@canastacr.com + 15 sample products) — these ran unconditionally
    // until 2026-06-16, which seeded them into the Azure DB the first time the API connected to
    // it. Gated here so a fresh non-dev database (Azure, CI, a new teammate's environment) never
    // gets fixture data mixed in with real data again.
    if (app.Environment.IsDevelopment())
    {
        await SeedDevUser(dbContext);
        await SeedSampleData(dbContext);
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task SeedDevUser(AppDbContext db)
{
    if (await db.Users.AnyAsync()) return;
    var user = new CanastaCR.Core.Entities.User
    {
        Id = Guid.NewGuid(),
        Email = "admin@canastacr.com",
        DisplayName = "Admin",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
        CreatedAt = DateTimeOffset.UtcNow
    };
    db.Users.Add(user);
    db.UserPreferences.Add(new CanastaCR.Core.Entities.UserPreferences { UserId = user.Id });
    await db.SaveChangesAsync();
}

static async Task SeedSampleData(AppDbContext db)
{
    if (await db.Products.AnyAsync()) return;

    var now = DateTimeOffset.UtcNow;
    var expires = now.AddDays(90);

    // Store IDs from the EF seed in AppDbContext
    var autoMercado = new Guid("11111111-0000-0000-0000-000000000001");
    var masXMenos   = new Guid("11111111-0000-0000-0000-000000000003");
    var maxiPali    = new Guid("11111111-0000-0000-0000-000000000005");
    var megaSuper   = new Guid("11111111-0000-0000-0000-000000000007");
    var priceSmart  = new Guid("11111111-0000-0000-0000-000000000009");

    var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@canastacr.com");

    // Products
    var leche         = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Leche Dos Pinos Entera", Brand = "Dos Pinos", Category = "Lácteos", Barcode = "7441001000001", CreatedAt = now };
    var arroz         = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Arroz Tío Pelón 1kg", Brand = "Tío Pelón", Category = "Granos", Barcode = "7441001000002", CreatedAt = now };
    var aceite        = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Aceite Clover 1L", Brand = "Clover", Category = "Aceites", Barcode = "7441001000003", CreatedAt = now };
    var frijoles      = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Frijoles Negros Camelia 500g", Brand = "Camelia", Category = "Granos", Barcode = "7441001000004", CreatedAt = now };
    var pan           = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Pan Bimbo Blanco 680g", Brand = "Bimbo", Category = "Panadería", Barcode = "7441001000005", CreatedAt = now };
    var lizano        = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Salsa Lizano 235ml", Brand = "Lizano", Category = "Salsas", Barcode = "7441001000006", CreatedAt = now };
    var atun          = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Atún Sardimar en Agua 140g", Brand = "Sardimar", Category = "Enlatados", Barcode = "7441001000007", CreatedAt = now };
    var azucar        = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Azúcar Blanca 1kg", Brand = "LAICA", Category = "Abarrotes", Barcode = "7441001000008", CreatedAt = now };
    var cafe          = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Café Britt Gourmet 250g", Brand = "Café Britt", Category = "Café", Barcode = "7441001000009", CreatedAt = now };
    var pasta         = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Pasta Carozzi Espagueti 400g", Brand = "Carozzi", Category = "Pastas", Barcode = "7441001000010", CreatedAt = now };
    var mantequilla   = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Mantequilla Dos Pinos 225g", Brand = "Dos Pinos", Category = "Lácteos", Barcode = "7441001000011", CreatedAt = now };
    var yogur         = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Yogur Dos Pinos Fresa 200g", Brand = "Dos Pinos", Category = "Lácteos", Barcode = "7441001000012", CreatedAt = now };
    var papel         = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Papel Higiénico Scott 4 rollos", Brand = "Scott", Category = "Limpieza", Barcode = "7441001000013", CreatedAt = now };
    var jabon         = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Jabón Palmolive 3 pack", Brand = "Palmolive", Category = "Limpieza", Barcode = "7441001000014", CreatedAt = now };
    var huevos        = new CanastaCR.Core.Entities.Product { Id = Guid.NewGuid(), Name = "Huevos Cinta Azul Docena", Brand = "Cinta Azul", Category = "Frescos", Barcode = "7441001000015", CreatedAt = now };

    db.Products.AddRange(leche, arroz, aceite, frijoles, pan, lizano, atun, azucar, cafe, pasta, mantequilla, yogur, papel, jabon, huevos);

    // Prices: (productId, storeId, price) — realistic CRC 2024 values
    // AutoMercado tends to be priciest; MaxiPalí/MegaSuper cheapest
    var prices = new[]
    {
        // Leche 1L
        (leche.Id,       autoMercado, 1250m), (leche.Id, masXMenos, 1090m), (leche.Id, maxiPali, 1025m), (leche.Id, megaSuper, 1065m), (leche.Id, priceSmart, 975m),
        // Arroz 1kg
        (arroz.Id,       autoMercado, 1150m), (arroz.Id, masXMenos, 990m),  (arroz.Id, maxiPali, 945m),  (arroz.Id, megaSuper, 975m),  (arroz.Id, priceSmart, 890m),
        // Aceite 1L
        (aceite.Id,      autoMercado, 2450m), (aceite.Id, masXMenos, 2190m),(aceite.Id, maxiPali, 2095m),(aceite.Id, megaSuper, 2145m),(aceite.Id, priceSmart, 1950m),
        // Frijoles 500g
        (frijoles.Id,    autoMercado, 1050m), (frijoles.Id, masXMenos, 895m),(frijoles.Id, maxiPali, 850m),(frijoles.Id, megaSuper, 875m),(frijoles.Id, priceSmart, 820m),
        // Pan
        (pan.Id,         autoMercado, 1850m), (pan.Id, masXMenos, 1650m),   (pan.Id, maxiPali, 1595m),   (pan.Id, megaSuper, 1620m),
        // Lizano
        (lizano.Id,      autoMercado, 1750m), (lizano.Id, masXMenos, 1550m),(lizano.Id, maxiPali, 1475m),(lizano.Id, megaSuper, 1520m),(lizano.Id, priceSmart, 1395m),
        // Atún
        (atun.Id,        autoMercado, 895m),  (atun.Id, masXMenos, 750m),   (atun.Id, maxiPali, 715m),   (atun.Id, megaSuper, 735m),  (atun.Id, priceSmart, 680m),
        // Azúcar 1kg
        (azucar.Id,      autoMercado, 680m),  (azucar.Id, masXMenos, 595m), (azucar.Id, maxiPali, 565m), (azucar.Id, megaSuper, 580m),(azucar.Id, priceSmart, 545m),
        // Café Britt
        (cafe.Id,        autoMercado, 4950m), (cafe.Id, masXMenos, 4650m),  (cafe.Id, megaSuper, 4550m), (cafe.Id, priceSmart, 4250m),
        // Pasta
        (pasta.Id,       autoMercado, 850m),  (pasta.Id, masXMenos, 720m),  (pasta.Id, maxiPali, 690m),  (pasta.Id, megaSuper, 705m),
        // Mantequilla
        (mantequilla.Id, autoMercado, 1950m), (mantequilla.Id, masXMenos, 1750m),(mantequilla.Id, maxiPali, 1680m),(mantequilla.Id, megaSuper, 1715m),(mantequilla.Id, priceSmart, 1625m),
        // Yogur
        (yogur.Id,       autoMercado, 650m),  (yogur.Id, masXMenos, 545m),  (yogur.Id, maxiPali, 515m),  (yogur.Id, megaSuper, 530m),
        // Papel higiénico
        (papel.Id,       autoMercado, 2150m), (papel.Id, masXMenos, 1850m), (papel.Id, maxiPali, 1750m), (papel.Id, megaSuper, 1795m),(papel.Id, priceSmart, 1650m),
        // Jabón
        (jabon.Id,       autoMercado, 1450m), (jabon.Id, masXMenos, 1250m), (jabon.Id, maxiPali, 1190m), (jabon.Id, megaSuper, 1215m),(jabon.Id, priceSmart, 1095m),
        // Huevos
        (huevos.Id,      autoMercado, 2950m), (huevos.Id, masXMenos, 2650m),(huevos.Id, maxiPali, 2495m),(huevos.Id, megaSuper, 2550m),(huevos.Id, priceSmart, 2350m),
    };

    foreach (var (productId, storeId, price) in prices)
    {
        db.PriceReports.Add(new CanastaCR.Core.Entities.PriceReport
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            StoreId = storeId,
            Price = price,
            Currency = "CRC",
            Source = CanastaCR.Core.Enums.PriceSource.UserSubmitted,
            ReportedById = adminUser?.Id,
            ReportedAt = now.AddHours(-new Random().Next(1, 72)),
            ExpiresAt = expires,
        });
    }

    if (adminUser != null)
    {
        // Sample shopping list
        var list = new CanastaCR.Core.Entities.ShoppingList
        {
            Id = Guid.NewGuid(),
            UserId = adminUser.Id,
            Name = "Lista del mercado",
            CreatedAt = now,
        };
        db.ShoppingLists.Add(list);

        var listItems = new[] { leche, arroz, frijoles, pan, huevos, aceite };
        foreach (var (product, qty) in listItems.Select((p, i) => (p, (decimal)(i == 0 ? 2 : 1))))
        {
            db.ShoppingListItems.Add(new CanastaCR.Core.Entities.ShoppingListItem
            {
                Id = Guid.NewGuid(),
                ShoppingListId = list.Id,
                ProductId = product.Id,
                Quantity = qty,
                Unit = CanastaCR.Core.Enums.QuantityUnit.Unit,
                IsPurchased = false,
            });
        }

        // Pantry items
        var pantry = new[]
        {
            (leche,       2m,  3m,  now.AddDays(-5)),
            (arroz,       0.5m,1m,  now.AddDays(-20)),
            (frijoles,    1m,  2m,  now.AddDays(-15)),
            (aceite,      0.3m,1m,  now.AddDays(-30)),
            (cafe,        2m,  1m,  now.AddDays(-7)),
            (lizano,      1m,  1m,  now.AddDays(-45)),
            (papel,       6m,  4m,  now.AddDays(-10)),
        };

        foreach (var (product, qty, min, lastBought) in pantry)
        {
            db.PantryItems.Add(new CanastaCR.Core.Entities.PantryItem
            {
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                ProductId = product.Id,
                Quantity = qty,
                Unit = CanastaCR.Core.Enums.QuantityUnit.Unit,
                MinThreshold = min,
                LastPurchasedAt = lastBought,
                UpdatedAt = now,
            });
        }
    }

    await db.SaveChangesAsync();
}

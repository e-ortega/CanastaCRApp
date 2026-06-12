# CanastaCR — Claude Code briefing

## What this is
Costa Rica supermarket price comparison app. The user noticed ~10% price differences on identical items across stores and wants to help CR shoppers find the best prices, build optimized shopping lists, and track pantry stock.

**Target stores:** AutoMercado, Más x Menos, MaxiPalí, MegaSuper, PriceSmart

**Repo:** https://github.com/e-ortega/CanastaCRApp (private)
**Local:** `D:\Documents\source\repos\CanastaCRApp\`

---

## Architecture

Monorepo — two independent projects:

```
CanastaCRApp/
├── api/          ASP.NET Core 9 Web API
│   ├── src/
│   │   ├── CanastaCR.Core/           entities, enums, DTOs, interfaces
│   │   ├── CanastaCR.Infrastructure/ AppDbContext, EF migrations, OpenFoodFactsClient
│   │   └── CanastaCR.Api/            controllers, services, Program.cs
│   └── tests/
│       └── CanastaCR.Tests/          xUnit tests
└── app/          Flutter (Web POC → iOS → Android)
    ├── lib/
    │   ├── core/     api_client, models, router, theme
    │   ├── features/ auth, search, compare, shopping, pantry
    │   └── shared/   main_scaffold
    └── test/
        └── models/   unit tests
```

### Backend stack
- ASP.NET Core 9, 3-layer (Core / Infrastructure / Api)
- Entity Framework Core + Npgsql (PostgreSQL in production)
- EF Core InMemory provider for unit tests
- JWT Bearer auth + BCrypt password hashing
- xUnit test framework

### Flutter stack
- go_router for navigation
- provider for state management
- dio for HTTP
- flutter_secure_storage for JWT token
- `flutter_test` (SDK) for unit tests — no mocking library needed for model tests

### Flutter executable (Windows)
```
C:\Users\eorte\source\repos\flutter\bin\flutter
```
Always use this full path when running `flutter` commands via Bash tool.

---

## Data model (key entities)

| Entity | Purpose |
|---|---|
| `Product` | Barcode-anchored product identity |
| `Store` | 10 seeded CR store locations |
| `PriceReport` | Price at a store, expires after 90 days |
| `ShoppingList` + `ShoppingListItem` | User's shopping list |
| `PantryItem` | Home inventory with min threshold |
| `UserPreferences` | `travelCostThreshold` (₡2000), `maxStoresPerTrip` (2) |

Split-store optimizer: greedy algorithm assigns each item to its cheapest store, collapses to single store if savings < `travelCostThreshold`.

---

## API endpoints

```
Auth
  POST /api/auth/register
  POST /api/auth/login

Products
  GET  /api/products                   list all (top 50, with lowest price)
  GET  /api/products/search?q=         text search
  GET  /api/products/barcode/{code}    barcode lookup
  POST /api/products                   create product
  GET  /api/products/{id}/prices       get comparison for one product

Prices
  POST /api/prices                     report a price
  GET  /api/prices/savings             savings dashboard summary
  GET  /api/prices/store/{id}          prices at one store

Stores
  GET  /api/stores

Shopping lists
  GET    /api/shopping-lists
  POST   /api/shopping-lists
  DELETE /api/shopping-lists/{id}
  GET    /api/shopping-lists/{id}
  POST   /api/shopping-lists/{id}/items
  PATCH  /api/shopping-lists/{id}/items/{itemId}/purchased
  GET    /api/shopping-lists/{id}/optimize

Pantry
  GET   /api/pantry
  POST  /api/pantry
  PATCH /api/pantry/{id}/quantity
  DELETE /api/pantry/{id}
  POST  /api/pantry/add-low-stock-to-list/{listId}
```

---

## Running things

### API (development)
```bash
cd api
dotnet run --project src/CanastaCR.Api
# Swagger: https://localhost:7001/swagger
# Seed data loads automatically on first run (15 products, 5 stores, prices)
# Dev credentials: admin@canastacr.com / admin123
```

### API tests
```bash
cd api
dotnet test tests/CanastaCR.Tests/
# 28 tests — all should pass
```

### Flutter app
```bash
cd app
C:\Users\eorte\source\repos\flutter\bin\flutter run -d chrome
```

### Flutter tests
```bash
cd app
C:\Users\eorte\source\repos\flutter\bin\flutter test test/models/
# 30 tests — all should pass
```

---

## Test coverage

### Backend (28 xUnit tests)
- `AuthServiceTests` (6): register, login, hashing, duplicate email, wrong password
- `PriceServiceTests` (6): comparison ordering, savings calc, expired exclusion, savings summary
- `PantryServiceTests` (7): upsert create/update, running low detection, delete ownership
- `ShoppingServiceTests` (9): create list, add item, increment quantity, toggle purchased, optimize (assigns/collapses/empty), delete list

### Flutter (30 unit tests in `test/models/`)
- `product_test.dart` (4): Product + ProductSearchResult fromJson, null fields, numeric coercion
- `price_test.dart` (7): StorePrice + PriceComparison fromJson, expiry, null savings
- `shopping_test.dart` (11): ShoppingList/Item fromJson, pendingCount, OptimizationResult + StoreGroup parsing
- `pantry_test.dart` (8): PantryItem fromJson, stockPercent clamp/zero/division-by-zero

---

## Key architectural decisions (and why)

### EF Core InMemory — `AddItemAsync` uses FK-only path
`ShoppingService.AddItemAsync` does NOT use `list.Items.Add(item)` or set `Product = product` on new `ShoppingListItem`. It uses `db.ShoppingListItems.Add(item)` with only FK values set.

**Why:** EF Core InMemory provider throws `DbUpdateConcurrencyException` when navigation fixup touches the `User`↔`UserPreferences` one-to-one relationship (FK-as-PK) through the chain of loaded navigations. Using FK-only + reload-after-save avoids all tracking side-effects and is correct for production too.

### Test isolation pattern (ShoppingServiceTests)
Each test calls `SeedBase()` which creates a named in-memory DB, seeds via one context (then disposes it), and returns the DB name. The service is created with a **separate fresh context** pointing at the same DB name. This avoids change-tracking conflicts between seeding and service operations.

### Split-store collapse threshold
When the optimizer produces >1 store group but total savings < `travelCostThreshold`, it collapses to a single store and sets `TotalSavings = 0`. The "savings" figure is computed as (max-price-per-product) − (optimized total), not vs the cheapest single store — this is intentional, it represents worst-case vs best-case.

---

## Phase 1 status — COMPLETE ✓ (2026-06-11)

All features built, all tests passing, pushed to GitHub.

---

## Roadmap

### Pending before Phase 1 is "shipped"
- **Azure deployment** (see below)
- **GitHub Actions CI/CD** (see below)

### Phase 2 — Mobile + Pantry enhancements
- Flutter iOS build
- Barcode scanner (`mobile_scanner`)
- Price tag OCR (photo → Azure Computer Vision → parse → confirm)
- Receipt scan (bulk price submission)
- GPS store auto-detection
- Savings dashboard charts (monthly savings over time)
- Shopping trip recording

### Phase 3 — Automation + Intelligence
- Web scrapers (Playwright .NET, one per store, Hangfire nightly)
- Price history chart per product
- Price drop alerts (push notifications)
- Recommendation engine ("you're low on X and it's 15% off at Y")
- Android build

---

## Azure deployment plan (NOT YET DONE)

### Resources needed (~$28/mo)
| Resource | SKU | Cost |
|---|---|---|
| App Service Plan + Web App | B1 | ~$13/mo |
| PostgreSQL Flexible Server | B1ms, 32GB | ~$15/mo |
| Static Web Apps | Free tier | $0 |
| Application Insights | Pay-as-you-go | ~$0 (free 5GB) |

All under resource group `canastacr-rg`.

### App Service environment variables (set as App Settings)
```
ConnectionStrings__DefaultConnection   = <PostgreSQL connection string>
Jwt__Key                               = <long random secret>
Jwt__Issuer                            = canastacr
Jwt__Audience                          = canastacr
ASPNETCORE_ENVIRONMENT                 = Production
```

### GitHub Actions secrets needed
```
AZURE_WEBAPP_PUBLISH_PROFILE           (download from App Service → Publish profile)
AZURE_STATIC_WEB_APPS_API_TOKEN        (from Static Web App → Manage deployment token)
DATABASE_URL                           (PostgreSQL connection string, for EF migrations step)
```

### CI/CD workflows (to be created at `.github/workflows/`)
- `api-ci.yml`: triggers on `push` to `main` when `api/**` changes → restore/build/test → publish → run EF migrations → deploy to App Service
- `app-ci.yml`: triggers on `push` to `main` when `app/**` changes → `flutter build web --release --dart-define=API_URL=https://...` → deploy to Static Web Apps

### EF Core migrations
Migrations already exist at `api/src/CanastaCR.Infrastructure/Migrations/`.
Production deploy runs: `dotnet ef database update` against the PostgreSQL connection string.

---

## Things to always do in this project
- Run `dotnet test` before committing backend changes
- Run `flutter test test/models/` before committing Flutter changes
- Keep seed data idempotent (`if (await db.Products.AnyAsync()) return;` guard in `Program.cs`)
- API changes that affect DTOs must be reflected in the Flutter models (`lib/core/models/`)

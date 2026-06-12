# CanastaCR — Claude Code briefing

## What this is
Costa Rica supermarket price comparison app. The user noticed ~10% price differences on identical items across stores and wants to help CR shoppers find the best prices, build optimized shopping lists, and track pantry stock.

Core challenge: **data population** — not all stores have consistent or scrapable websites, so the strategy combines user crowdsourcing (Day 1), barcode/photo input (Phase 2), and periodic web scraping (Phase 3).

**Repo:** https://github.com/e-ortega/CanastaCRApp (private monorepo)
**Local:** `D:\Documents\source\repos\CanastaCRApp\`

---

## Target stores

| Store | Website | Notes |
|---|---|---|
| AutoMercado | automercado.co.cr | Full online store — most scrapable |
| Más x Menos | masxmenos.cr | Walmart CR subsidiary |
| MaxiPalí | maxipali.co.cr | Walmart CR subsidiary |
| MegaSuper | megasuper.net | Online store available |
| PriceSmart | pricesmart.com | Membership club, may require login |

10 store locations are seeded in `AppDbContext.SeedStores()` via `HasData`.

---

## Architecture

```
┌──────────────────────────────────────┐
│           Flutter App                │
│  (Web POC → iOS → Android)           │
│  • Barcode scan (mobile_scanner)     │
│  • Photo capture (price tag/receipt) │
│  • Price submission + comparison     │
│  • Shopping lists + optimizer        │
│  • Pantry inventory                  │
└──────────────┬───────────────────────┘
               │ REST / JSON
┌──────────────▼───────────────────────┐
│       ASP.NET Core 9 Web API         │
│  • Product lookup & search           │
│  • Price CRUD + savings summary      │
│  • Shopping list + optimizer         │
│  • Pantry CRUD                       │
│  • Auth (JWT + BCrypt)               │
└──────┬───────────────┬───────────────┘
       │               │
┌──────▼──────┐  ┌─────▼───────────────────┐
│ PostgreSQL  │  │  Background Scrapers     │
│ (main DB)   │  │  (Hangfire + Playwright) │
│             │  │  Nightly, per store      │
└─────────────┘  └──────────────────────────┘
```

### Monorepo layout

```
CanastaCRApp/
├── api/
│   ├── src/
│   │   ├── CanastaCR.Core/           entities, enums, DTOs, interfaces
│   │   ├── CanastaCR.Infrastructure/ AppDbContext, EF migrations, OpenFoodFactsClient
│   │   └── CanastaCR.Api/            controllers, services, Program.cs
│   └── tests/
│       └── CanastaCR.Tests/          xUnit unit tests
└── app/
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
- EF Core InMemory provider for unit tests only
- JWT Bearer auth + BCrypt password hashing
- xUnit + EF InMemory for unit tests
- Hangfire (Phase 3) — background scraper scheduler
- Playwright for .NET (Phase 3) — headless browser scraping

### Flutter stack
- go_router (navigation), provider (state), dio (HTTP)
- flutter_secure_storage for JWT token persistence
- mobile_scanner (Phase 2) — barcode/QR scanning
- camera (Phase 2) — price tag / receipt photo capture
- `flutter_test` SDK for unit tests — no mocking library needed for pure model tests

### Flutter executable (Windows)
```
C:\Users\eorte\source\repos\flutter\bin\flutter
```
Always use this full path in Bash tool calls.

---

## Data model

```
Product
  id, barcode (UPC/EAN, unique nullable), name, brand, category,
  imageUrl, description, createdAt

Store
  id, name, chain (enum), address, lat, lng, city
  → 10 locations seeded via HasData

PriceReport
  id, productId → Product, storeId → Store,
  price (decimal 12,2), currency (CRC default),
  source (enum: UserSubmitted | Scraped),
  reportedBy → User (nullable — null for scraper),
  reportedAt, expiresAt (90 days default)

User
  id, email (unique), displayName, passwordHash,
  reputationPoints (incentivize crowdsourcing), createdAt

UserPreferences           ← PK = UserId (one-to-one with User)
  userId, travelCostThreshold (₡2000 default),
  maxStoresPerTrip (2 default), currency, homeLat, homeLng

ShoppingList
  id, userId, name, createdAt

ShoppingListItem
  id, shoppingListId, productId, quantity (decimal 10,3),
  unit (enum: Unit | Kg | Liter), isPurchased

ShoppingTrip              ← recorded when user checks out
  id, userId, storeId, totalSpent, estimatedSavings, date

PantryItem
  id, userId, productId, quantity (decimal 10,3), unit,
  minThreshold (decimal 10,3), lastPurchasedAt, updatedAt
```

---

## API endpoints

```
Auth
  POST /api/auth/register   { email, displayName, password }
  POST /api/auth/login      { email, password }

Products
  GET  /api/products                   list all (top 50, with lowest price)
  GET  /api/products/search?q=         text search
  GET  /api/products/barcode/{code}    barcode lookup → Open Food Facts fallback
  POST /api/products                   create product manually
  GET  /api/products/{id}/prices       full price comparison for one product

Prices
  POST /api/prices                     report a price (authenticated)
  GET  /api/prices/savings             savings dashboard summary
  GET  /api/prices/store/{id}          all prices at one store

Stores
  GET  /api/stores

Shopping lists (all authenticated)
  GET    /api/shopping-lists
  POST   /api/shopping-lists
  DELETE /api/shopping-lists/{id}
  GET    /api/shopping-lists/{id}
  POST   /api/shopping-lists/{id}/items
  PATCH  /api/shopping-lists/{id}/items/{itemId}/purchased
  GET    /api/shopping-lists/{id}/optimize

Pantry (all authenticated)
  GET    /api/pantry
  POST   /api/pantry
  PATCH  /api/pantry/{id}/quantity
  DELETE /api/pantry/{id}
  POST   /api/pantry/add-low-stock-to-list/{listId}
```

---

## Product identification — three paths

### Path 1: Barcode scan (primary — Phase 2)
1. User scans barcode with camera
2. API looks up barcode in local DB
3. If not found → query **Open Food Facts API** (free, 3M+ products, has CR products)
4. If still not found → create stub product, ask user to confirm name/photo
5. User selects current store → enters price → submits

> Barcode is on the product itself (not the price tag), so the same code works across all stores. This is the reliable identity anchor.

### Path 2: OCR photo of price tag (Phase 2)
1. User photographs shelf label
2. API sends to **Azure Computer Vision** (or Google Vision) for OCR
3. Text parsed for price using regex patterns per store's label format
4. User confirms extracted price before submitting

### Path 3: Manual entry (already built — Phase 1)
1. User types product name → search returns matches from DB
2. User selects product → enters store + price → submits
3. If not found → "Add product manually" sheet (name, brand, category, barcode optional)

### Bonus: Receipt scan — bulk entry (Phase 2)
1. User photographs full receipt after checkout
2. OCR extracts item names + prices
3. App fuzzy-matches items to products in DB
4. One confirm tap submits all prices at once
> High-value path — one receipt populates 20+ prices in seconds and auto-updates pantry.

---

## Price data population strategy

### Tier 1: User crowdsourcing (Day 1 — already implemented)
- Every price submission builds the DB
- Gamification: `reputationPoints` per verified submission, leaderboard (Phase 2)
- Staleness: reports older than 90 days excluded from comparisons; users prompted to re-verify

### Tier 2: Web scraping (Phase 3 — nightly)
- One scraper class per store implementing `IStoreScraper`:
```csharp
interface IStoreScraper {
    Task<IEnumerable<ScrapedProduct>> ScrapeAsync(CancellationToken ct);
}
// Implementations: AutoMercadoScraper, MasXMenosScraper, MaxiPaliScraper, etc.
```
- Playwright handles JS-rendered storefronts (Angular/React)
- Scraped prices stored with `source = Scraped` — lower confidence than user reports
- If scraped price differs >5% from latest user report → flag for review
- Respect `robots.txt`; randomized delays; run 2–4 AM Costa Rica time (UTC-6)
- AutoMercado is the easiest target (full online store, clean HTML)
- Más x Menos + MaxiPalí share Walmart CR infrastructure — one scraper may cover both

### Tier 3: Price alerts (Phase 3)
- User subscribes to a product → push notification when price drops or scrape detects change

---

## Smart shopping features

### Split-store optimizer (built)
- For each unpurchased item, finds lowest current price across all stores
- Groups by store, up to `maxStoresPerTrip`
- If total savings across groups < `travelCostThreshold` → collapses to single store, `TotalSavings = 0`
- Output: "Buy at AutoMercado: [items] — Buy at MaxiPalí: [items]" with cost breakdown

### Savings dashboard (built — basic)
- `GET /api/prices/savings` returns `SavingsSummaryDto`: total products with price gap, total potential savings, avg savings %, top 5 deals
- Phase 2: monthly savings chart (line), store comparison pie, "biggest deal found" highlight

### Pantry inventory (built)
- `isRunningLow = quantity < minThreshold`
- `stockPercent = (quantity / minThreshold).clamp(0, 1)` — drives progress bar in UI
- "Add all low-stock items to list" button → `POST /pantry/add-low-stock-to-list/{listId}`
- Phase 2: auto-update pantry when a ShoppingTrip is marked complete

### Recommendation engine (Phase 3)
- "You usually buy X — it's 15% cheaper at AutoMercado this week"
- "Your pantry shows you haven't bought cooking oil in 30 days"
- Seasonal patterns: "Tomatoes are cheapest in November based on historical data"

---

## Running things

### API (development — InMemory DB, no PostgreSQL needed)
```bash
cd api
dotnet run --project src/CanastaCR.Api
# Swagger UI: https://localhost:7068/swagger
# Seed data loads automatically on first run (15 products, prices at all 5 chains)
# Dev credentials: admin@canastacr.com / admin123
```

### API tests
```bash
cd api
dotnet test tests/CanastaCR.Tests/
# 28 tests — all should pass
```

### Flutter app (web)
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

### Backend (28 xUnit tests — all passing)
- `AuthServiceTests` (6): register creates user+token, password hashed (BCrypt), duplicate email → null, login returns token, wrong password → null, nonexistent user → null
- `PriceServiceTests` (6): comparison ordered cheapest-first, savings amount/% calc, expired prices excluded, null for missing product, summary returns deals, summary excludes expired
- `PantryServiceTests` (7): upsert creates item, upsert updates (no duplicate), upsert null for missing product, GetItems marks running low, GetItems doesn't mark when at threshold, delete removes, delete returns false for wrong user
- `ShoppingServiceTests` (9): create list, add item, increment quantity on duplicate, toggle purchased/unpurchased, optimize assigns to cheapest store, optimize collapses when savings < threshold, optimize empty when all purchased, delete list removes list+items, delete returns false for wrong user

### Flutter (30 unit tests — all passing, in `test/models/`)
- `product_test.dart` (4): Product + ProductSearchResult fromJson, null fields, int→double coercion
- `price_test.dart` (7): StorePrice + PriceComparison fromJson, expiry flag, null savings when single store
- `shopping_test.dart` (11): ShoppingList/Item fromJson, `pendingCount` computed getter, OptimizationResult + StoreGroup + OptimizedItem parsing, empty groups
- `pantry_test.dart` (8): PantryItem fromJson, null lastPurchasedAt, int coercion, `stockPercent` ratio/clamp/zero/division-by-zero

---

## Key architectural decisions (and why)

| Decision | Choice | Reason |
|---|---|---|
| Product identity anchor | Barcode (EAN/UPC) | Universal, store-agnostic, machine-readable |
| Product metadata source | Open Food Facts first, user fill-in second | Free, global, 3M+ products including CR |
| Scraping engine | Playwright (.NET) | CR store sites are JS-heavy (Angular/React) |
| Price staleness | 90-day expiry | Grocery prices shift weekly; older data is misleading |
| Currency | CRC (colones) default | Local market; USD toggle as future option |
| Auth | JWT Bearer + BCrypt | Simple, stateless; social login optional later |
| Deployment | Azure (App Service + PostgreSQL Flexible + Static Web Apps) | User has Azure subscription; chosen over Railway/Render |

### EF Core InMemory — `AddItemAsync` uses FK-only path
`ShoppingService.AddItemAsync` does NOT use `list.Items.Add(item)` or set `Product = product` navigation on new `ShoppingListItem`. Uses `db.ShoppingListItems.Add(item)` with FK values only + reload after save.

**Why:** EF Core InMemory throws `DbUpdateConcurrencyException` when navigation fixup touches the `User`↔`UserPreferences` one-to-one (FK-as-PK) relationship through the chain of loaded navigations. FK-only + reload avoids all tracking side-effects and is correct for production too.

### Test isolation pattern
Each ShoppingService test calls `SeedBase()` → creates named in-memory DB → seeds via one context (then **disposes** it) → returns DB name. Service gets a **separate fresh context** pointing at the same DB name. This avoids change-tracking conflicts between the seeding context and the service under test.

### Split-store savings calculation
`savings = singleStoreCost - totalCost` where `singleStoreCost` = sum of MAX price per product (theoretical worst case), `totalCost` = sum of cheapest prices in the assigned store groups. This represents worst-case vs best-case, not vs a specific single store.

---

## Phase status

### Phase 1 — Web POC ✅ COMPLETE (2026-06-11)
- [x] .NET API: Products, Stores, PriceReports CRUD
- [x] Seed DB with 15 common CR products + all 5 store chains (10 locations)
- [x] Flutter Web: product search, price comparison table, manual price submission
- [x] Shopping list: create/delete list, add products, toggle purchased, split-store optimizer
- [x] User accounts + JWT auth
- [x] Pantry: CRUD, running-low detection, add-to-list
- [x] Savings dashboard (basic — summary endpoint + home screen banner)
- [x] Manual product creation (search screen + sheet)
- [x] 28 backend unit tests + 30 Flutter unit tests — all passing
- [x] Monorepo pushed to GitHub (e-ortega/CanastaCRApp)
- [ ] **Deploy to Azure** (next step)
- [ ] **GitHub Actions CI/CD** (next step)
- [ ] Open Food Facts barcode integration (scaffolded, not wired to UI yet)

### Phase 2 — Mobile (iOS) + richer features
- [ ] Flutter iOS build + TestFlight
- [ ] Barcode scanner (`mobile_scanner` package)
- [ ] Price tag OCR (photo → Azure Computer Vision → parse → user confirms)
- [ ] Receipt scan flow (bulk price submission + auto-update pantry)
- [ ] GPS store auto-detection (suggest nearest store when submitting price)
- [ ] Pantry auto-update when ShoppingTrip is marked complete
- [ ] Savings dashboard charts (monthly line chart, store pie)
- [ ] Reputation points + leaderboard for crowdsourced prices
- [ ] ShoppingTrip recording (track actual spend vs estimate)

### Phase 3 — Automation + Intelligence
- [ ] Web scrapers: AutoMercado (priority), MaxiPalí/Más x Menos (Walmart CR, shared infra)
- [ ] Hangfire recurring jobs (nightly, 2–4 AM CR time)
- [ ] Price history chart per product
- [ ] Price drop alerts (push notifications for items on shopping list or subscribed products)
- [ ] Recommendation engine
- [ ] Android build

---

## Azure deployment plan (PENDING)

### Resources (~$28/mo total)
| Resource | SKU | Cost |
|---|---|---|
| App Service Plan + Web App | B1 (1 vCore, 1.75 GB) | ~$13/mo |
| PostgreSQL Flexible Server | B1ms (1 vCore, 2 GB, 32 GB) | ~$15/mo |
| Static Web Apps | Free tier | $0 |
| Application Insights | Pay-as-you-go | ~$0 (free 5 GB/mo) |

All under resource group: `canastacr-rg`

### App Service Application Settings (env vars at runtime)
```
ConnectionStrings__DefaultConnection = <PostgreSQL connection string>
Jwt__Key                             = <long random secret ≥32 chars>
Jwt__Issuer                          = canastacr
Jwt__Audience                        = canastacr
ASPNETCORE_ENVIRONMENT               = Production
```

### GitHub Actions secrets needed
```
AZURE_WEBAPP_PUBLISH_PROFILE          (App Service → Download publish profile)
AZURE_STATIC_WEB_APPS_API_TOKEN       (Static Web App → Manage deployment token)
DATABASE_URL                          (PostgreSQL connection string, for EF migrations)
```

### CI/CD workflows (to be created at `.github/workflows/`)
- **`api-ci.yml`** — triggers on push to `main` when `api/**` changes:
  `restore → build → test → publish → ef database update → azure/webapps-deploy`
- **`app-ci.yml`** — triggers on push to `main` when `app/**` changes:
  `flutter pub get → flutter test → flutter build web --release --dart-define=API_URL=https://canastacr-api.azurewebsites.net → Azure/static-web-apps-deploy`

### EF Core migrations
Already generated at `api/src/CanastaCR.Infrastructure/Migrations/`.
Deploy step runs: `dotnet ef database update` against production PostgreSQL connection string.

---

## Always do in this project
- `dotnet test` before committing backend changes
- `flutter test test/models/` before committing Flutter changes
- Keep `SeedSampleData()` in `Program.cs` idempotent (`if (await db.Products.AnyAsync()) return;`)
- API DTO changes must be reflected in Flutter models (`app/lib/core/models/`)
- New API endpoints need the corresponding Flutter API client call in `api_client.dart`

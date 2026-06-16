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
| MegaSuper | megasuper.com | Next.js SSR, JSON-LD prices baked in, EAN in URL |
| PriceSmart | pricesmart.com/en-cr | Nuxt.js + CommerceTools, open API, 12K products |

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
  → 12 locations seeded via HasData

PriceReport
  id, productId → Product,
  storeId → Store (nullable) / chain (enum, nullable) — exactly one is set, never both:
    UserSubmitted reports carry a real storeId (a shopper observed this at a specific
    location); Scraped reports carry chain instead (every chain scraped so far prices
    uniformly nationwide, not per location — see docs/ARCHITECTURE.md section 11),
  price (decimal 12,2), currency (CRC default),
  source (enum: UserSubmitted | Scraped),
  reportedBy → User (nullable — null for scraper),
  reportedAt, expiresAt (90 days UserSubmitted / 3 days Scraped default)

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

## Environment status

Living record of what's installed on each machine. Update this whenever a tool is installed or changes.

### macOS — MacBook Air (estebanortega, darwin-arm64, macOS 26.5)

| Tool | Status | Notes |
|---|---|---|
| Flutter | ✅ 3.44.2 stable | On PATH via Homebrew |
| Chrome | ✅ | Web target working |
| Xcode | ✅ 26.5 | Simulator runtimes downloaded — flutter doctor clean |
| CocoaPods | ✅ 1.16.2 | |
| Android Studio / SDK | ➖ Not installed (by design) | Android work done on Windows machine instead |
| .NET SDK | ✅ 9.0.315 + 10.0.301 | Project targets net9.0, resolved by 9.0.315 |
| pwsh (PowerShell) | ✅ 7.6.2 | |
| Azure CLI | ✅ 2.87.0 | |
| GitHub CLI (gh) | ✅ 2.94.0 | |
| Git hooks | ✅ | Active — tests run automatically before each commit |

### Windows — (eorte, Windows)

| Tool | Status | Notes |
|---|---|---|
| Flutter | ✅ | `C:\Users\eorte\source\repos\flutter\bin\flutter` |
| Xcode | N/A | iOS dev is macOS-only |
| Android Studio / SDK | ❓ | |
| .NET 9 SDK | ✅ | Original dev machine |
| pwsh | ✅ | |
| Azure CLI | ✅ | |
| GitHub CLI (gh) | ✅ | |

---

## Platform detection

Before running any command, detect the OS:

```bash
uname -s   # Darwin = macOS | Linux = Linux
```
In PowerShell: `$IsMacOS` / `$IsWindows` are built-in booleans in PowerShell Core (pwsh).

### macOS
- Flutter: `flutter` (on PATH after install — see macOS setup below)
- PowerShell: `pwsh` (`brew install powershell`)
- Scripts: `pwsh scripts/run.ps1 <command>`
- Environment vars: set in `~/.zshrc` or `~/.zprofile`

### Windows (eorte)
- Flutter: `C:\Users\eorte\source\repos\flutter\bin\flutter`
- PowerShell: `powershell` or `pwsh`
- Scripts: `.\scripts\run.ps1 <command>`
- Environment vars: Windows user environment variables (persist across terminals)

The `$Flutter` variable in `scripts/run.ps1` auto-selects the correct path via `$IsWindows`.  
Override on any machine by setting env var `CANASTACR_FLUTTER=/path/to/flutter`.

---

## macOS environment setup (new machine)

Run these once on a new Mac. All commands use Homebrew.

### 1. Homebrew
```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

### 2. Core tools
```bash
brew install git gh azure-cli powershell
```
- `gh` — GitHub CLI (needed for `sync-issues.ps1`, `setup-github.ps1`)
- `azure-cli` — Azure CLI (needed for infra commands)
- `powershell` — installs `pwsh`, required by `run.ps1` and the git hooks

### 3. .NET 9 SDK
```bash
brew install --cask dotnet-sdk
dotnet --version   # should show 9.x
```

### 4. Flutter (stable) ✅ done
```bash
brew install --cask flutter
flutter doctor     # shows what's still missing
```
Flutter will be on PATH automatically after Homebrew install.

### 5. Xcode + iOS toolchain ✅ done
```bash
# 1. Ensure Xcode command-line tools point to the app
sudo xcode-select --switch /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -license accept

# 2. Download iOS simulator runtime (missing — causes flutter doctor warning)
#    Option A: Xcode → Settings (Cmd+,) → Platforms → + → iOS
#    Option B: command line:
sudo xcodebuild -downloadPlatform iOS

# 3. CocoaPods — Flutter's iOS dependency manager
brew install cocoapods

# 4. Verify Flutter sees everything
flutter doctor
```
After step 2, `flutter doctor` Xcode row should show ✓. Update Environment status table above.

Open iOS Simulator: `open -a Simulator` (comes with Xcode)

### 6. PostgreSQL (local dev — optional)
API uses EF InMemory by default in dev mode, so PostgreSQL is only needed if you switch to a local DB.
```bash
brew install postgresql@16
brew services start postgresql@16
```

### 7. Environment variables (macOS)
Add to `~/.zshrc` (or `~/.zprofile` for login shells):
```bash
# CanastaCR — local dev only (PostgreSQL, overrides appsettings.Development.json)
export ConnectionStrings__DefaultConnection="Host=localhost;Database=canastacr_dev;Username=postgres;Password=<your-pg-password>"

# Azure / infra commands
export AZURE_RESOURCE_GROUP="canastacr-rg"
export POSTGRES_ADMIN_PASSWORD="<prod-db-password>"
export JWT_SECRET="<jwt-secret-32-chars>"
```
Then `source ~/.zshrc` to apply.

### 8. Activate git hooks
```bash
pwsh scripts/run.ps1 hooks:install
```

---

## Running things

### API (development — InMemory DB, no PostgreSQL needed)
```bash
# via task runner (both platforms)
pwsh scripts/run.ps1 api          # macOS
.\scripts\run.ps1 api             # Windows

# or directly
cd api && dotnet run --project src/CanastaCR.Api --launch-profile https
# Swagger UI: https://localhost:7068/swagger
# Seed data loads automatically on first run (15 products, prices at all 5 chains)
# Dev credentials: admin@canastacr.com / admin123
```

### API tests
```bash
pwsh scripts/run.ps1 api:test     # macOS
.\scripts\run.ps1 api:test        # Windows
```

### Flutter app (web)
```bash
pwsh scripts/run.ps1 app          # macOS
.\scripts\run.ps1 app             # Windows
```

### Flutter app (iOS Simulator — macOS only)
```bash
open -a Simulator                 # launch iOS Simulator first
cd app && flutter run             # auto-selects the simulator
# or target it explicitly:
flutter run -d "iPhone 16"
```

### Flutter tests
```bash
pwsh scripts/run.ps1 app:test     # macOS
.\scripts\run.ps1 app:test        # Windows
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

### Phase 1 — Web POC ✅ COMPLETE (2026-06-12)
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
- [x] Deploy to Azure (Canada Central — App Service B1 + PostgreSQL B1ms + SWA Free)
- [x] GitHub Actions CI/CD (manual trigger — api.yml, app.yml, infra.yml)
### Phase 2 — Mobile (iOS) + richer features
- [ ] Flutter iOS build + TestFlight
- [ ] Barcode scanner (`mobile_scanner` package)
- [ ] Open Food Facts barcode integration (API scaffolded — wire to Flutter barcode scanner UI)
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

## Azure deployment (LIVE)

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

## Architectural journal

`docs/ARCHITECTURE.md` is a living architectural decision log. **Update it automatically** whenever:
- A meaningful architectural decision is made (tech choice, schema change, scraper strategy, infra option chosen)
- A new API endpoint or external service is discovered and confirmed working
- A decision is reversed or superseded
- A new future opportunity or product idea is discussed

When updating:
- Add to the relevant existing section if one fits; create a new section if not
- Include: what was decided, why, what was ruled out, date
- Add new open questions to section 6 as they surface
- Do NOT rewrite existing entries — append or extend them
- The owner may also edit this file manually at any time

**Do not wait to be asked** — if you make an architectural decision during a session, update the journal before ending the session.

---

## Always do in this project

### MANDATORY — one-time setup after cloning
```powershell
.\scripts\run.ps1 hooks:install
```
**Do not skip this.** It activates two git hooks stored in `.githooks/`:

| Hook | When it runs | What it does |
|---|---|---|
| `pre-commit` | Before every `git commit` | Runs `api:test` if `api/` is staged; runs `app:analyze` + `app:test` if `app/` is staged. Blocks the commit if anything fails. |
| `post-commit` | After every `git commit` | If `CLAUDE.md` was committed: creates GitHub Issues for any new `- [ ]` items added to Phase 2/3; closes GitHub Issues for any items changed to `- [x]`. |

To disable: `.\scripts\run.ps1 hooks:uninstall`

### MANDATORY before any commit touching `api/`
```powershell
.\scripts\run.ps1 api:test
# All 28 tests must pass. Do not commit if any test fails.
# (Enforced automatically by pre-commit hook if hooks:install was run.)
```

### MANDATORY before any commit touching `app/`
```powershell
.\scripts\run.ps1 app:analyze
.\scripts\run.ps1 app:test
# 0 lint issues, all 30 tests must pass. Do not commit if either fails.
# (Enforced automatically by pre-commit hook if hooks:install was run.)
```

### Other rules
- Keep `SeedSampleData()` in `Program.cs` idempotent (`if (await db.Products.AnyAsync()) return;`)
- API DTO changes must be reflected in Flutter models (`app/lib/core/models/`)
- New API endpoints need the corresponding Flutter API client call in `api_client.dart`


### Competition Apps
- I have found i'm not the one doing this idea, we should take this into consideration and develop a strategy to take advatange or to differentiate from them
- https://www.mimejorcompracr.go.cr/
- https://www.ahorraya.app/
- https://www.buscadorprecios.com/
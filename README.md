# CanastaCR

Costa Rica supermarket price comparison app. Compare prices across AutoMercado, Más x Menos, MaxiPalí, MegaSuper, and PriceSmart — build optimized shopping lists, track pantry stock, and find the best deals.

## Live

| | URL |
|---|---|
| Flutter Web | https://agreeable-desert-0fe200510.7.azurestaticapps.net |
| API (Swagger) | https://canastacr-api-prod.azurewebsites.net/swagger |

Dev account: `admin@canastacr.com` / `admin123`

---

## Architecture

```
┌──────────────────────────────────────┐
│           Flutter App                │
│  (Web POC → iOS → Android)           │
│  • Price comparison                  │
│  • Shopping lists + optimizer        │
│  • Pantry inventory                  │
└──────────────┬───────────────────────┘
               │ REST / JSON
┌──────────────▼───────────────────────┐
│       ASP.NET Core 9 Web API         │
│  • Products, Prices, Stores          │
│  • Shopping lists + optimizer        │
│  • Pantry CRUD                       │
│  • JWT auth                          │
└──────┬───────────────────────────────┘
       │
┌──────▼──────────┐
│  PostgreSQL 16  │  (Azure Flexible Server, Canada Central)
│  + EF Core      │
└─────────────────┘
```

### Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 9, EF Core, Npgsql |
| Frontend | Flutter 3, go_router, provider, dio |
| Database | PostgreSQL 16 (prod) / EF InMemory (tests) |
| Auth | JWT Bearer + BCrypt |
| IaC | Azure Bicep |
| CI/CD | GitHub Actions |
| Hosting | Azure App Service B1 + Static Web Apps Free |

---

## Monorepo layout

```
CanastaCRApp/
├── api/
│   ├── src/
│   │   ├── CanastaCR.Core/           entities, enums, DTOs, interfaces
│   │   ├── CanastaCR.Infrastructure/ AppDbContext, EF migrations, OpenFoodFactsClient
│   │   └── CanastaCR.Api/            controllers, services, Program.cs
│   └── tests/
│       └── CanastaCR.Tests/          28 xUnit tests
├── app/
│   ├── lib/
│   │   ├── core/                     api_client, models, router, theme
│   │   ├── features/                 auth, search, compare, shopping, pantry
│   │   └── shared/                   main_scaffold
│   └── test/
│       └── models/                   30 Flutter unit tests
├── infra/                            Azure Bicep IaC
│   ├── main.bicep
│   ├── main.bicepparam
│   └── modules/
│       ├── appservice.bicep
│       ├── keyvault.bicep
│       ├── monitoring.bicep
│       ├── postgres.bicep
│       └── staticwebapp.bicep
├── scripts/                          PowerShell task runner + tooling
│   ├── run.ps1                       ← main task runner
│   ├── check-region.ps1
│   ├── setup-github.ps1
│   └── sync-issues.ps1
└── .github/
    ├── workflows/
    │   ├── api.yml                   API build + deploy
    │   ├── app.yml                   Flutter build + deploy
    │   └── infra.yml                 Bicep deploy (manual)
    └── dependabot.yml
```

---

## Local development

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Flutter](https://docs.flutter.dev/get-started/install) (stable)
- PostgreSQL 16 (local instance)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) (for infra commands)
- [GitHub CLI](https://cli.github.com/) (for `sync-issues`, `setup-github`)

### Environment variables

Set these once as Windows user environment variables (persist across terminals):

```powershell
# PostgreSQL connection string for local dev (overrides appsettings.Development.json)
[System.Environment]::SetEnvironmentVariable(
  'ConnectionStrings__DefaultConnection',
  'Host=localhost;Database=canastacr_dev;Username=postgres;Password=<your-pg-password>',
  'User'
)

# Required for infra commands
[System.Environment]::SetEnvironmentVariable('AZURE_RESOURCE_GROUP', 'canastacr-rg', 'User')
[System.Environment]::SetEnvironmentVariable('POSTGRES_ADMIN_PASSWORD', '<prod-db-password>', 'User')
[System.Environment]::SetEnvironmentVariable('JWT_SECRET', '<jwt-secret-32-chars>', 'User')
```

Restart your terminal after setting these.

### One-time setup (after cloning)

```powershell
# Activate pre-commit hook — blocks commits if tests fail
.\scripts\run.ps1 hooks:install
```

### Quick start

```powershell
# Run API locally (http://localhost:5098 + https://localhost:7068/swagger)
.\scripts\run.ps1 api

# Run Flutter app in Chrome (connects to http://localhost:5098 by default)
.\scripts\run.ps1 app

# Run all tests
.\scripts\run.ps1 test
```

---

## Task runner — `scripts/run.ps1`

Single entry point for all common tasks. Usage: `.\scripts\run.ps1 <command>`

### API

| Command | Description |
|---|---|
| `api` | Run API with launch profile `https` (Swagger at https://localhost:7068/swagger) |
| `api:watch` | Run API with hot-reload |
| `api:build` | Build Release configuration |
| `api:test` | Run 28 xUnit tests |
| `api:publish` | Publish to `api/publish/` |

### Flutter

| Command | Description |
|---|---|
| `app` | Run Flutter app in Chrome |
| `app:build` | `flutter build web --release` (uses `API_URL` env var or default localhost) |
| `app:test` | Run 30 Flutter unit tests |
| `app:analyze` | `flutter analyze` |

### Infrastructure

| Command | Description |
|---|---|
| `infra:validate` | Validate Bicep without deploying |
| `infra:deploy` | Deploy Bicep to Azure (requires env vars) |
| `infra:outputs` | Show outputs from last deployment |
| `infra:stop` | Stop App Service + PostgreSQL to save cost (~$28/mo while stopped) |
| `infra:start` | Start App Service + PostgreSQL |
| `infra:delete` | Delete entire resource group (prompts for confirmation by name) |
| `infra:delete-resource` | Delete a single resource by ID |
| `log:tail` | Stream live App Service logs (`Ctrl+C` to stop) |

### Combined

| Command | Description |
|---|---|
| `test` | Run backend + Flutter tests together |

---

## Scripts

### `scripts/check-region.ps1`

Checks Azure resource availability for a given region before deploying. Useful when choosing or switching regions.

```powershell
.\scripts\check-region.ps1 -Location canadacentral
.\scripts\check-region.ps1 -Location eastus2
```

Checks: App Service, PostgreSQL Flexible Server (including `OfferRestricted` flag), Static Web Apps, Key Vault, Log Analytics, Application Insights.

### `scripts/setup-github.ps1`

One-time repository security setup. Run once after creating the repo (requires repo to be public or GitHub Pro).

```powershell
.\scripts\setup-github.ps1           # apply changes
.\scripts\setup-github.ps1 -DryRun  # preview without applying
```

Configures:
- Branch protection on `main` (PRs required, CI must pass, no force-push)
- Secret scanning + push protection
- Dependabot vulnerability alerts

### `scripts/sync-issues.ps1`

Reads unchecked `- [ ]` items from Phase 2 and Phase 3 in `CLAUDE.md` and creates matching GitHub Issues. Safe to run repeatedly — skips items that already have an open issue.

```powershell
.\scripts\sync-issues.ps1           # create issues
.\scripts\sync-issues.ps1 -DryRun  # preview without creating
```

Labels issues as `phase-2` / `phase-3` + `enhancement`.

---

## CI/CD workflows

All workflows are **manual trigger only** (`workflow_dispatch`). Run from **GitHub → Actions → [workflow] → Run workflow**.

### `api.yml` — API build + deploy

1. Restore → Build (Release) → Test (28 xUnit)
2. Publish + deploy to `canastacr-api-prod` App Service via publish profile

**Required secret:** `AZURE_WEBAPP_PUBLISH_PROFILE`

### `app.yml` — Flutter build + deploy

1. `flutter pub get` → `flutter analyze` → `flutter test test/models/`
2. `flutter build web --release --dart-define=API_URL=<prod-url>`
3. Deploy to `canastacr-web-prod` Static Web App

**Required secrets:** `AZURE_STATIC_WEB_APPS_API_TOKEN`, `API_URL`

### `infra.yml` — Bicep infrastructure deploy

Deploys all Azure resources via `infra/main.bicep`. Idempotent — safe to re-run.

**Required secrets:** `POSTGRES_ADMIN_PASSWORD`, `JWT_SECRET`
> Note: OIDC login is not configured (guest user in host tenant). Run infra manually via `.\scripts\run.ps1 infra:deploy` instead.

---

## Azure infrastructure

All resources in resource group `canastacr-rg`.

| Resource | Name | SKU | Region | Monthly cost |
|---|---|---|---|---|
| App Service Plan | `canastacr-api-prod-plan` | B1 (1 vCore, 1.75 GB) | Canada Central | ~$13 |
| App Service | `canastacr-api-prod` | — | Canada Central | included |
| PostgreSQL Flexible | `canastacr-db-prod` | B1ms (1 vCore, 2 GB, 32 GB) | Canada Central | ~$15 |
| Static Web Apps | `canastacr-web-prod` | Free | Central US | $0 |
| Key Vault | `canastacr-kv-prod` | Standard | Canada Central | ~$0 |
| Application Insights | `canastacr-ai-prod` | Pay-as-you-go (5 GB free) | Canada Central | ~$0 |
| Log Analytics | `canastacr-logs-prod` | Pay-as-you-go | Canada Central | ~$0 |

**Total: ~$28/mo.** Use `.\scripts\run.ps1 infra:stop` when not in use.

### Bicep modules

| Module | Creates |
|---|---|
| `modules/appservice.bicep` | App Service Plan (B1 Linux) + Web App + app settings |
| `modules/postgres.bicep` | PostgreSQL Flexible Server B1ms, version 16 |
| `modules/keyvault.bicep` | Key Vault (RBAC mode) + `jwt-secret` + `db-connection-string` |
| `modules/monitoring.bicep` | Log Analytics Workspace + Application Insights |
| `modules/staticwebapp.bicep` | Static Web Apps Free tier |

Secrets are injected at deploy time via `readEnvironmentVariable()` in `main.bicepparam` — never hardcoded.

---

## API endpoints

```
Auth
  POST /api/auth/register      { email, displayName, password }
  POST /api/auth/login         { email, password } → { token, displayName, userId }
  GET  /api/users              List all users (authenticated)

Products
  GET  /api/products                    List all (top 50, with lowest price)
  GET  /api/products/search?q=          Text search
  GET  /api/products/barcode/{code}     Barcode lookup → Open Food Facts fallback
  POST /api/products                    Create product manually
  GET  /api/products/{id}/prices        Full price comparison for one product

Prices
  POST /api/prices                      Report a price (authenticated)
  GET  /api/prices/savings              Savings dashboard summary
  GET  /api/prices/store/{id}           All prices at one store

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

## Roadmap

### Phase 1 — Web POC ✅ Complete
API + Flutter Web deployed to Azure. Shopping lists, price comparison, pantry, JWT auth, 58 tests passing.

### Phase 2 — Mobile + richer features
iOS build, barcode scanner, price tag OCR, receipt scan, GPS store detection, pantry auto-update, savings charts, reputation system.

### Phase 3 — Automation + Intelligence
Nightly web scrapers (AutoMercado priority), Hangfire jobs, price history, push alerts, recommendation engine, Android build.

> Run `.\scripts\sync-issues.ps1` to create GitHub Issues for all Phase 2 and 3 work items.

---

## Seeded data

On first boot, the API seeds:
- **15 common CR products** (Dos Pinos milk, Tío Pelón rice, Salsa Lizano, etc.)
- **10 store locations** across all 5 chains
- **~70 price reports** with realistic 2024 CRC prices
- **Dev user** `admin@canastacr.com` / `admin123` with a sample shopping list and pantry

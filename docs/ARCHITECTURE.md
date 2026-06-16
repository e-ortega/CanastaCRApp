# CanastaCR — Architectural Journal

> This is a living document. Claude updates it automatically when architectural decisions are made during development sessions. The owner can also add entries manually at any time.
>
> Format: each decision records **what** was chosen, **why**, **what was ruled out**, and **what to revisit at scale**.

---

## Table of Contents

1. [Store Scraper Research & API Discovery](#1-store-scraper-research--api-discovery)
2. [Product Identity & Deduplication](#2-product-identity--deduplication)
3. [Database Architecture](#3-database-architecture)
4. [Scraper as Microservice / Data Product](#4-scraper-as-microservice--data-product)
5. [Future Opportunities & SaaS Ideas](#5-future-opportunities--saas-ideas)
6. [Open Questions & Things to Revisit](#6-open-questions--things-to-revisit)

---

## 1. Store Scraper Research & API Discovery

**Date:** 2026-06-15  
**Status:** Research complete, implementation pending

### Findings per store

| Store | Tech Stack | API Type | Auth Required | Scraper Needed | Complexity |
|---|---|---|---|---|---|
| MaxiPalí | VTEX | Open REST | None | HttpClient | Low |
| Más x Menos | VTEX (same platform) | Open REST | None | HttpClient | Low |
| Walmart CR | VTEX (same platform) | Open REST | None | HttpClient | Low |
| PriceSmart | Nuxt.js + CommerceTools | Open POST API | None | HttpClient | Low |
| MegaSuper | Next.js SSR | JSON-LD in HTML | None | HttpClient + HTML parse | Very Low |
| AutoMercado | Angular PWA + Firebase | Firestore (real-time) | None | Playwright | High |

### Confirmed API endpoints (no auth, tested)

**MaxiPalí & Más x Menos (VTEX):**
```
GET https://www.maxipali.co.cr/api/catalog_system/pub/category/tree/3
    → full 3-level category tree with IDs

GET https://www.maxipali.co.cr/api/catalog_system/pub/products/search
    ?fq=C:{categoryId}&_from=0&_to=49
    → paginated products with EAN, Price, ListPrice, brand, images, IsAvailable

# Same endpoints work for masxmenos.cr and walmart.co.cr — same VTEX platform
# walmart.co.cr confirmed 2026-06-15: category tree returns 19 top-level categories
```

**PriceSmart (CommerceTools via Nuxt proxy):**
```
POST https://www.pricesmart.com/api/ct/getProduct
     { "country": "CR", "offset": 0, "limit": 50 }
     → 12,021 products total, paginated
     → price in centAmount (÷100 = CRC price)

POST https://www.pricesmart.com/api/ct/getProduct
     { "slug": "members-selection-100-pure-sunflower-oil-5-l-1-32-gal-167401", "country": "CR" }
     → single product with price
```

**MegaSuper (Next.js SSR JSON-LD):**
```
GET https://www.megasuper.com/p/{slug}-{ean}
    → HTML page with <script type="application/ld+json"> containing:
      - name, sku (= EAN), brand
      - offers.priceSpecification.price (CRC)
      - offers.availability (InStock/OutOfStock)
      - offers.priceValidUntil
      - images (https://info.megasuper.com/ecommerce/{ean}_{1-4}.jpeg)

# Product enumeration: GET /sitemap.xml → extract all /p/ URLs
# EAN is the last segment of the URL slug — no separate field needed
```

**AutoMercado:**
```
GET https://automercado.azure-api.net/prod-front/home/subMenu
    → full 3-level category tree (no auth, confirmed open)

# Product data lives in Firebase Firestore — requires Playwright to render
# Firebase project: app-auto-mercado
# Store selection stored in localStorage key: ts-t
```

### robots.txt summary

| Store | Product catalog blocked? | Notable restrictions |
|---|---|---|
| MaxiPalí / Más x Menos | No | /account/, /login/, /checkout/, /quick-view/ |
| PriceSmart | No | Standard account/checkout paths |
| MegaSuper | No | `Allow: /` — fully open, has LLM-Policy |
| AutoMercado | No | /perfil/, /checkout/, /content/ only |

### Decision: scraper implementation hierarchy

```
IStoreScraper
├── VtexScraper           → MaxiPalí + Más x Menos (1 class, 2 registrations, param by base URL)
├── CommerceToolsScraper  → PriceSmart
├── JsonLdScraper         → MegaSuper (fetch HTML → parse JSON-LD → no browser)
└── PlaywrightScraper     → AutoMercado (deferred — only one requiring headless browser)
```

**Why this order:** Ship v1 scraper without Playwright entirely. 4 stores covered by pure HTTP means faster, cheaper, more reliable scraping. Playwright added in v2 for AutoMercado only.

---

## 2. Product Identity & Deduplication

**Date:** 2026-06-15  
**Status:** Strategy defined, implementation pending

### The core problem

"Aceite Girasol La Favorita 1L" at AutoMercado and "ACEITE DE GIRASOL LA FAVORITA 1 LT" at MaxiPalí are the same physical product. Bad matching = duplicates in the DB = wrong price comparisons.

### Strategy: EAN-first, fuzzy fallback

```
Priority 1: EAN (barcode)
  UPSERT ON CONFLICT (ean) → guaranteed no duplicates
  Available from: MaxiPalí (ean field), Más x Menos (ean field),
                  MegaSuper (ean = sku = in URL)

Priority 2: Open Food Facts lookup
  When scraper returns no EAN → query OFF by name+brand → get canonical EAN
  If found → use EAN, back to Priority 1

Priority 3: Fuzzy name match
  normalize(name) + brand match against existing products
  normalize = lowercase + strip accents + collapse units ("1 lt"/"1L" → "1l") + trim stopwords
  Confidence score:
    ≥ 0.90 → auto-merge as same product
    0.70–0.90 → insert into scraper.staging_products for human review
    < 0.70 → create new product (assume different)

Priority 4: Manual review queue
  scraper.staging_products table — flagged for human confirmation
  UI needed: "Is this the same product?" → merge or keep separate
```

### EAN availability per store

| Store | EAN available? | Notes |
|---|---|---|
| MaxiPalí / Más x Menos | ✅ `ean` field in VTEX | Direct |
| MegaSuper | ✅ EAN = SKU = in URL | Trivial |
| PriceSmart | ⚠️ Partial | Internal SKU is their item number, not EAN. Attributes may contain EAN. To verify. |
| AutoMercado | ❓ Unknown | Needs Playwright scrape of a product page to confirm |

### Implementation note

Deduplication logic lives entirely in `PriceWriterService`, not in individual scrapers. Scrapers are dumb — they return `ScrapedProduct` records. The writer normalizes, matches, and decides.

**To revisit at scale:** When catalog grows >100K products, fuzzy matching becomes expensive. Move to vector embeddings (product name → embedding → cosine similarity lookup) for matching. pgvector extension on PostgreSQL handles this without a separate service.

---

## 3. Database Architecture

**Date:** 2026-06-15  
**Status:** Option A in use now; Option B is the planned next step

### The three options evaluated

**Option A — Shared DB, same schema (current)**
```
[Scraper] ──writes──▶ [PostgreSQL: app schema] ◀──reads── [Main API]
```
- ✅ Zero complexity, immediate consistency
- ✅ Right for nightly batch load (not continuous writes)
- ❌ Schema coupled — changes affect both
- ❌ Scraper bulk writes can cause lock contention during peak API usage
- **Use until:** scraper runs > 1/day OR write volume causes measurable API latency

**Option B — Same DB, separate schemas (next step)**
```
[Scraper] ──▶ [scraper.*: raw_products, raw_prices, staging_products]
                     │ ETL job (nightly, after scrape)
                     ▼
              [app.*: products, price_reports] ◀── [Main API]
```
- ✅ Scraper and app decoupled at schema level
- ✅ ETL is where deduplication/normalization lives
- ✅ Single DB server — no cross-service complexity
- ❌ ETL adds latency (prices land in app tables ~hours after scrape)
- **Migrate to when:** Option A causes noticeable coupling or lock problems

**Option C — Separate databases**
```
[Scraper] ──▶ [Scraper DB] ──API/events──▶ [App DB] ◀── [Main API]
```
- ✅ Full independence — different tech, different scaling
- ❌ Data sync complexity, eventual consistency
- **Migrate to when:** building multi-tenant SaaS or scraper serves multiple consumers

### Decision: start with A, keep B migration clean

Key principle adopted: **even in Option A, the scraper uses a dedicated `ScraperDbContext`** that only exposes the tables it needs (Products, Stores, PriceReports). It never touches User, ShoppingList, Pantry tables. This enforces logical decoupling without operational complexity, and makes the A→B migration mechanical.

### TimescaleDB

`PriceReport` is time-series data. TimescaleDB is a PostgreSQL extension (same connection string, same EF Core, no code changes) that:
- Compresses historical price data 90%+
- Handles range queries (price history charts) faster
- Enables automatic partitioning by date

**Decision:** Enable TimescaleDB when setting up the scraper PostgreSQL, before the first scraper run. Easier to add before data exists than after.

Rough data growth:
```
5 stores × 10,000 products × 365 days = 18M PriceReport rows/year
With TimescaleDB compression: ~1.8M equivalent rows
```

---

## 4. Scraper as Microservice / Data Product

**Date:** 2026-06-15  
**Status:** Architecture defined, scaffold pending

### Is it a microservice?

Yes — it fits the definition cleanly:
- **Single responsibility:** scrapes and writes prices, nothing else
- **Independent lifecycle:** deploy, restart, scale without touching the main API
- **Shared storage as the integration contract:** writes PriceReport rows that the main API reads
- **No synchronous coupling:** main API never calls the scraper; scraper never calls the main API

This is the classic **ETL microservice / data pipeline** pattern.

### Deployment options (cheapest → most capable)

| Option | Cost | Best for |
|---|---|---|
| Local / Task Scheduler | $0 | Dev + personal use |
| Azure Container Apps Job | ~$0–2/mo | Nightly scheduled, scales to zero |
| Azure Functions (Container) | ~$0–3/mo | Timer trigger, pay-per-execution |
| Azure Container Instance | ~$0.001/min | One-shot runs, billed by the minute |
| Azure App Service (shared) | Shares existing cost | Hangfire on same plan as main API |

**Decision:** Start local (Task Scheduler or `.\run.ps1 scraper:run`). Add Azure Container Apps Job when ready for cloud — designed exactly for "run a container on a schedule, pay only for execution time."

### Project structure

```
scraper/
  src/CanastaCR.Scraper/
    Abstractions/
      IStoreScraper.cs            ← interface (input: CancellationToken, output: ScrapedProduct[])
      IScrapeResultStore.cs       ← interface for writing results (swap sink without changing scrapers)
      ScrapedProduct.cs           ← shared record all scrapers produce
    Scrapers/
      VtexScraper.cs              ← HttpClient, parameterized by base URL
      CommerceToolsScraper.cs     ← HttpClient, PriceSmart
      JsonLdScraper.cs            ← HttpClient + AngleSharp HTML parse, MegaSuper
      AutoMercadoScraper.cs       ← Playwright (deferred)
    Services/
      PriceWriterService.cs       ← implements IScrapeResultStore, writes to PostgreSQL
      ProductMatcherService.cs    ← EAN-first + fuzzy match + staging queue logic
    Jobs/
      ScrapeAllStoresJob.cs       ← Hangfire job, calls all registered IStoreScraper
    Program.cs                    ← Worker Service + Hangfire + DI + ScraperDbContext
```

### The `IScrapeResultStore` abstraction

This is the key seam for future flexibility:

```csharp
public interface IScrapeResultStore
{
    Task WriteAsync(IEnumerable<ScrapedProduct> products, string storeName, CancellationToken ct);
}

// Today: writes to PostgreSQL via ScraperDbContext
// Future option 1: publishes to Azure Service Bus (multiple consumers)
// Future option 2: writes to Scraper DB (Option C migration)
// Future option 3: exposes as REST API endpoint that any consumer pulls from
```

---

## 5. Future Opportunities & SaaS Ideas

**Date:** 2026-06-15  
**Added by:** Claude (from architectural discussion with owner)

> These are not commitments — they are captured to avoid losing the ideas. The scraper data is the moat. The app is just the first consumer. Over time, the same data can power very different products.

### The core asset

A continuously-updated, cross-store product price database for Costa Rica, keyed by EAN, with full history. Nobody else has this at scale in CR.

### Product ideas

| Product | Target buyer | Value proposition |
|---|---|---|
| **Price intelligence API** | Retailers & brands | "What does my competitor charge for this SKU across all CR chains?" |
| **Brand performance dashboard** | CPG companies (Nestlé, Unilever, P&G distributors) | "How does my brand price vs competitor brands across AutoMercado, MaxiPalí, PriceSmart?" |
| **Inflation tracker / market index** | Economists, journalists, INE (Costa Rica stats office) | Real-time grocery basket price index — faster than official CPI |
| **B2B procurement optimizer** | Restaurants, hotels, food service businesses | "You buy 50 kg of rice/month — here's the cheapest store today and projected price next week" |
| **Open dataset** | Researchers, developers, public good | Publish anonymized price history as open data — builds brand + community trust |
| **Embedded "lowest price" widget** | Recipe apps, nutrition apps, meal planners | API widget: given an ingredient list → show cheapest basket in CR |
| **Franchise benchmarking** | Franchise operators with multiple locations | "Store X is overpaying for category Y vs stores in the same chain" |

### Distribution / monetization paths

- **Data API (B2B):** monthly subscription per tier (calls/mo), API keys, rate limiting
- **Dashboard SaaS:** CPG companies pay per brand tracked
- **Data licensing:** sell historical exports to research firms
- **White-label:** another app embeds the price data under their brand

### What makes this defensible

1. **Historical depth** — prices from Day 1. Competitors starting later never catch up on history
2. **EAN-keyed** — cross-store matching at product level (not just name matching) is hard to replicate
3. **CR-specific** — a global player (Nielsen, Euromonitor) won't invest in CR market specifically; local data is the moat
4. **Network effect** — user-submitted prices + scraper prices = more complete than either alone

### When to start thinking about this

When scraper is stable and covering 4+ stores consistently (3–6 months of clean history). At that point, the data has enough depth to be a real commercial asset.

---

## 6. Open Questions & Things to Revisit

| Question | Context | Priority |
|---|---|---|
| PriceSmart EAN field | Their SKU (`167401`) is internal, not EAN. Do their product attributes contain a UPC/EAN? Needs one product attribute inspection. | High — affects deduplication |
| AutoMercado product page structure | Need to Playwright-render a product page to see field names, EAN availability, and reliable CSS selectors | Medium — deferred to Playwright phase |
| MegaSuper sitemap completeness | Confirmed `sitemap.xml` exists. Does it list ALL products or just featured ones? | High — affects enumeration strategy |
| AutoMercado store selection | Store 03 is pre-selected via `ts-t` localStorage. Do prices vary by store? If yes, need to scrape per-store. | Medium |
| PriceSmart pagination | Confirmed 12,021 products with offset/limit. What is the max limit per call? 50? 100? | Low |
| TimescaleDB setup | Enable before first scraper run on both local dev PostgreSQL and Azure PostgreSQL Flexible Server | Medium |
| Scraper rate limits | No rate limiting observed on any store API. Still: add 1–2 sec delay between pages, identify with User-Agent string, run 2–4 AM CR time | High — ethical + stability |
| Legal review | No ToS found for AutoMercado or MegaSuper at standard URLs. Should formally check before commercial use of data. | Medium for personal use, High for SaaS |

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
7. [Incident: First Production Run Wrote Zero Rows](#7-incident-first-production-run-wrote-zero-rows)
   - [7.1 PriceSmart real schema (reference)](#71-pricesmart-real-schema-reference)
   - [7.2 MegaSuper research findings — enumeration still broken, deferred](#72-megasuper-research-findings-2026-06-16--enumeration-still-broken-deferred)
8. [Live Test Framework](#8-live-test-framework)
9. [Observability & Operations](#9-observability--operations)

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

> **2026-06-16 correction:** the schemas documented below for PriceSmart and MegaSuper were wrong, and the VTEX EAN location was wrong — none were re-verified against a raw response before the scraper was implemented. The implementation built on these notes silently scraped zero rows from every store on its first run. See [Section 7](#7-incident-first-production-run-wrote-zero-rows) for the live-confirmed corrections. The blocks below are kept as-found (with corrections noted inline) so the mistake stays visible.

**MaxiPalí & Más x Menos (VTEX):**
```
GET https://www.maxipali.co.cr/api/catalog_system/pub/category/tree/3
    → full 3-level category tree with IDs

GET https://www.maxipali.co.cr/api/catalog_system/pub/products/search
    ?fq=C:{categoryId}&_from=0&_to=49
    → paginated products with EAN, Price, ListPrice, brand, images, IsAvailable
    → CORRECTION (2026-06-16): "EAN" here is NOT a top-level product field.
      It only exists at items[0].ean (per-SKU). Top-level product object has
      no "ean" key at all — confirmed live against maxipali.co.cr.

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

# CORRECTION (2026-06-16): the response is NOT { "results": [...] } at the root.
# Live-confirmed real shape:
#   { "data": { "products": { "offset", "count", "total": 12021, "results": [
#       { "id", "masterData": { "current": {
#           "slug": "<plain string, not a localized map>",
#           "name": "<plain string, not a localized map>",
#           "allVariants": [ { "id", "sku", "price": null (sample), "attributesRaw": [...] } ]
#       } } }
#   ] } } }
# RESOLVED 2026-06-16 — see section 7.1 for the full schema reference:
# price lives in the "unit_price" attributesRaw entry (JSON-string-encoded,
# per-country/per-club), never as a direct field. No EAN/UPC exists anywhere
# in the product data (confirmed across custom_attributes, all locales).
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
#
# CORRECTION (2026-06-16): /sitemap.xml now returns the Next.js SPA HTML shell,
# not XML — same response as /robots.txt, both apparently hit a catch-all route.
# UPDATE (2026-06-16, after deeper research — see section 7.2): the JSON-LD
# extraction itself turned out to still work fine; the script tag just has its
# attributes in a different order than a naive regex expected, but AngleSharp's
# real CSS selector handles that correctly. The ONLY confirmed blocker is
# enumeration — no working sitemap or server-rendered link source was found.
# MegaSuper appears to be migrating to the Instaleap platform; finding the real
# data source needs a browser with devtools, not curl. Deferred alongside
# AutoMercado as a Playwright follow-up.
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
| ~~PriceSmart EAN field~~ | Resolved 2026-06-16 — confirmed no EAN/UPC field exists anywhere in PriceSmart's product data (checked custom_attributes across all locales). PriceSmart products always go through fuzzy matching. | Closed |
| AutoMercado product page structure | Need to Playwright-render a product page to see field names, EAN availability, and reliable CSS selectors | Medium — deferred to Playwright phase |
| MegaSuper enumeration strategy | No working sitemap or server-rendered link source found (section 7.2). Needs Playwright/devtools investigation to find the real data source — likely an Instaleap API. | High — blocks MegaSuper entirely |
| AutoMercado store selection | Store 03 is pre-selected via `ts-t` localStorage. Do prices vary by store? If yes, need to scrape per-store. | Medium |
| PriceSmart full-catalog-scan cost | No server-side country filter exists (confirmed — tried `country`, `club`, `channelKey` params, none changed the `total`). ~3GB/run estimated. Look for a cheaper/filtered endpoint, or accept the cost for nightly batch. | Medium — works today, optimize before treating as high-frequency |
| TimescaleDB setup | Enable before first scraper run on both local dev PostgreSQL and Azure PostgreSQL Flexible Server | Medium |
| Scraper rate limits | No rate limiting observed on any store API. Still: add 1–2 sec delay between pages, identify with User-Agent string, run 2–4 AM CR time | High — ethical + stability |
| Legal review | No ToS found for AutoMercado or MegaSuper at standard URLs. Should formally check before commercial use of data. | Medium for personal use, High for SaaS |
| No raw/checkpoint storage | Scrapers hold all parsed `ScrapedProduct`s in memory for an entire store before any DB write happens; nothing is persisted mid-scrape. A crash mid-store loses that store's whole run; nothing can resume without re-scraping from scratch. | Medium — acceptable for now at current run times, revisit if a single store scrape exceeds ~10 min |
| No persistent log sink | Console-only logging — failure detail doesn't survive past the terminal session. The result-summary fix (section 7) covers counts, but not root-cause detail for failures. | Medium |

---

## 7. Incident: First Production Run Wrote Zero Rows

**Date:** 2026-06-16
**Status:** VTEX (Walmart/MaxiPalí/Más x Menos) and PriceSmart fixed and verified; MegaSuper deferred to a Playwright follow-up (section 7.1)

### Symptom

Owner triggered `nightly-scrape` manually via Hangfire. Job showed **Succeeded** after 30m 15s. No new products or price reports appeared in the database — only the 16 manually-seeded Phase 1 products were present afterward.

This is possible because `ScrapeAllStoresJob.RunAsync` and `PriceWriterService.WriteAsync` both catch exceptions broadly and only log them — neither failure propagates to Hangfire, and the job's return type (`Task`) carries no result for Hangfire to display. **"Succeeded" only means no exception escaped `RunAsync` — it says nothing about whether any row was written.**

### Root causes found (live-verified against the real sites on 2026-06-16)

1. **VTEX EAN read from the wrong JSON level.** `VtexScraper.ParseProduct` reads `item.ean` at the top product level. The real VTEX response only has `ean` nested inside `items[0].ean` (the SKU/variant level) — confirmed live against `maxipali.co.cr`. Top-level `ean` does not exist. Result: every VTEX product (MaxiPalí, Más x Menos, Walmart) gets `Barcode = null`.

2. **`ProductMatcherService` fuzzy-match path is not EF-translatable.** When `Barcode` is null, `FindOrCreateProductAsync` runs:
   ```csharp
   db.Products.Where(p => p.Name.ToLower().Contains(Normalize(scraped.Name)...))
   ```
   `Normalize()` uses `Regex.Replace` and Unicode normalization — EF Core cannot translate this into SQL and throws `InvalidOperationException` at runtime. Combined with bug #1, **every VTEX product hits this and throws**, is caught by `PriceWriterService`'s per-product try/catch, logged, and skipped. Net effect: zero VTEX rows written.

3. **`CommerceToolsScraper` reads the wrong response shape.** Code expects `{ "results": [...] }` at the root. The live PriceSmart response is actually shaped `{ "data": { "products": { "results": [...], "total": 12021 } } }`, and each result is `{ "id", "masterData": { "current": { "slug", "name", "allVariants": [{ "sku", "price", "attributesRaw" }] } } }` — not the `name`/`slug` as localized maps, nor `masterVariant`/`attributes` assumed in code. `json.TryGetProperty("results", ...)` fails immediately, the pagination loop breaks on page 1, zero PriceSmart products parsed. The original endpoint research (section 1) documented a simplified/incorrect shape that was never re-verified against a raw response before implementation.

4. **MegaSuper `/sitemap.xml` no longer returns XML.** Live request now returns the Next.js SPA HTML shell (same content as `/robots.txt` — both appear to hit a catch-all route), not a sitemap. `JsonLdScraper.GetProductUrlsFromSitemap`'s `XmlDocument.LoadXml` throws, caught, zero product URLs discovered. Separately, a known product page no longer exposes a clean `<script type="application/ld+json">` tag — the page now contains a `BAILOUT_TO_CLIENT_SIDE_RENDERING` marker, suggesting MegaSuper changed its Next.js rendering strategy since the original research. Both the enumeration strategy and the parse strategy for MegaSuper need to be re-investigated from scratch, not just patched.

### Why this stayed invisible for 30 minutes

All four scrapers actually executed real HTTP traffic against real store sites (hence the realistic run time), and every failure path was an exception caught by a `catch (Exception ex) { logger.LogError(...); }` block — by design, so one bad product or one bad store doesn't kill the whole nightly run. The cost of that resilience choice is that **total failure looks identical to total success** unless someone reads the console/log output at the time it happened. No log file sink is configured (`Console` only), so once the terminal session ended, the detailed error trail was gone — only the Hangfire "Succeeded" status and total duration survived.

### Fixes applied (2026-06-16)

- **VtexScraper**: EAN now read from `items[0].ean`. Covers Walmart, MaxiPalí, Más x Menos.
- **VtexScraper category strategy**: was recursing to LEAF categories (~860 of them for MaxiPalí) instead of using top-level ones (~26). VTEX's `fq=C:{id}` filter matches a category AND all its descendants, so top-level IDs already cover the whole catalog — leaf-level iteration was both unnecessary and, worse, mostly wasted: live-checked the first 10 leaf categories in tree order and all 10 returned zero products, while every sampled top-level category returned products immediately. This wasn't just slow — it's why the live smoke test (section 8) initially timed out at 60s per store despite the scraper being otherwise correct after the EAN fix. Switching to top-level categories took each VTEX store's 25-product smoke test from a 60s timeout down to under 1 second.
- **ProductMatcherService**: `Normalize()` now runs client-side before the query is built; the `Where()` predicate only ever sees a plain captured string. Also fixed the candidate-search heuristic itself — it used to search for a prefix of the *normalized* name as a substring of the *raw* name, which fails whenever a stopword (e.g. "de") appears before the 10th character, since normalization removes stopwords and shifts later words together while the raw name doesn't. Now anchors on the first raw word instead, which is never a stopword. Added a SQLite-backed regression test (`ProductMatcherServiceMatchingTests`) specifically because EF's `InMemory` test provider does **not** enforce SQL-translation rules and would not have caught the original bug — SQLite does, since it's a real relational provider.
- **CommerceToolsScraper**: fully rewritten against the live-confirmed schema (section 7.1 below has the schema details). Also discovered the listing endpoint returns PriceSmart's entire multi-country catalog with no server-side country filter — every product must be fetched and checked client-side for a Costa Rica price entry. ~80% of the catalog isn't sold in CR.
- **Job result visibility**: `ScrapeAllStoresJob.RunAsync` now returns `IReadOnlyList<StoreScrapeResult>` (scraped/written/skipped/failed counts and any error, per store). Hangfire displays this as the job's "Result" on the job detail page — "Succeeded" with this present now actually means something, instead of only meaning "no exception escaped."
- **Per-platform trigger filtering**: discovered the existing `/api/scrape/vtex`, `/megasuper`, `/pricesmart` HTTP endpoints all silently enqueued the *same* "scrape everything" job regardless of which URL was hit — the platform name in the URL was cosmetic. Added a `Platform` tag to `IStoreScraper` ("vtex"/"megasuper"/"pricesmart") and a `platform` filter parameter on `RunAsync` so these endpoints now actually scope to the right scrapers.
- **maxProducts cap**: `IStoreScraper.ScrapeAsync` takes an optional `maxProducts` parameter (and the trigger endpoints take a `?maxProducts=` query param) so a scrape can stop after N products instead of running the full catalog — used by the live test framework (section 8) and for fast manual smoke tests via Postman.

### Still open

- No persistent log sink (Console only) — a failure's detail still only exists for as long as the terminal/process stays up. The new result-summary return value covers the *counts*, but not *why* something failed beyond the exception message captured per scraper.
- PriceSmart full-catalog-scan cost: ~3GB of transfer for a complete nightly run (estimated from per-product payload size × 12,021 total catalog entries), since there's no known server-side country filter. Not yet a problem at current scale, but worth finding a cheaper endpoint before this runs daily long-term — see section 7.1.

---

### 7.1 PriceSmart real schema (reference)

Confirmed live 2026-06-16 against `POST https://www.pricesmart.com/api/ct/getProduct`:

```
{ data: { products: { offset, count, total: 12021, results: [
  { id, masterData: { current: {
      slug: "<plain string>",            ← NOT a localized {es,en} map
      name: "<plain string>",            ← NOT a localized {es,en} map, often has trailing spaces
      allVariants: [{
        id, sku: "167401",               ← internal item number, NOT an EAN/UPC
        attributesRaw: [
          { name: "unit_price", value: "<JSON-encoded string>", attributeDefinition: { type: { name: "text" } } },
          { name: "brand", value: "Member's Selection", attributeDefinition: { type: { name: "text" } } },
          { name: "localized_images", value: [{ "es-CR": "https://...", "en-CR": "https://...", ... }], attributeDefinition: { type: { name: "set" } } },
          ... 25+ more (department_description_es, weight, custom_attributes, etc.)
        ],
        availability: { channels: { results: [
          { channel: { key: "6408", address: { country: "CR" }, nameAllLocales: [...] },
            availability: { isOnStock: true, availableQuantity: 594 } },
          ... one entry per club/warehouse, across ALL countries PriceSmart operates in
        ] } }
      }]
  } } }
] } } }
```

Key gotchas:
- **`price` is always `null`** on `allVariants[0].price` and `masterVariant.price`, in both bulk-listing and single-slug lookups. The real price lives inside the `unit_price` *attribute*, not as a direct field.
- **Attribute `value` types differ by `attributeDefinition.type.name`.** `"text"`-typed attributes (unit_price, product_availability, original_price_without_saving, weight, ...) double-encode their value as a JSON string — must `JsonDocument.Parse()` it again. `"set"`-typed attributes (localized_images) store the value as an already-structured array — parsing it as a string will throw.
- **The catalog is global, not CR-scoped.** `country`/`club`/`channelKey` request body params do not filter results server-side (all tried, all returned the same `total: 12021`) — confirmed by sampling: 3 consecutive early-page products had zero CR entries; a sample at offset 500 had 4/20 (20%) with CR pricing. Every product must be fetched and checked client-side via `unit_price` entries where `country == "CR"`.
- **No EAN/UPC anywhere.** Checked `custom_attributes` (a per-locale JSON blob with ~20 PIM fields: form, vegan, kosher, organic, storage, allergens, etc.) — no upc/ean/gtin/barcode key exists in any locale variant inspected. PriceSmart products will always go through `ProductMatcherService`'s fuzzy-match path, never the exact-EAN path.
- **Country-aggregate club code.** Within the per-country price/availability entries, one club equals the country code itself (e.g. club `"64"` for country `"CR"`) — this is the country-wide aggregate, used in preference to picking an arbitrary per-store club entry.

### 7.2 MegaSuper research findings (2026-06-16) — enumeration still broken, deferred

Investigated from scratch since both `/sitemap.xml` and `/robots.txt` now return the Next.js SPA HTML shell instead of their expected formats (section 1 correction). Findings:

- **The JSON-LD extraction logic is NOT broken** — this corrects the original incident write-up. A known product page (`/p/leche-condensada-la-lechera-100-g-8445290709509`) still contains a real, well-formed `<script id="product-structured-data" type="application/ld+json">{...}</script>` tag with all the expected fields. The earlier "no clean JSON-LD tag" finding was a false negative from a naive regex that assumed `type` was the script tag's first attribute — the real tag has `id` first, which AngleSharp's `QuerySelector("script[type='application/ld+json']")` handles correctly (attribute order doesn't matter to a real CSS selector). `JsonLdScraper.ScrapeProductPage` should work unmodified once given a valid product URL.
- **The enumeration problem is real and the only confirmed blocker.** No sitemap variant works (`sitemap.xml`, `sitemap_index.xml`, `sitemap-0.xml`, `product-sitemap.xml`, `sitemap/sitemap.xml`, `sitemaps/sitemap.xml` — all either 404 or return the SPA shell). The homepage has zero server-rendered `<a>` tags at all (`grep -c '<a '` = 0) and a `BAILOUT_TO_CLIENT_SIDE_RENDERING` marker — category navigation and the product grid are client-rendered, not present in the initial HTML response. The product page itself has no server-rendered breadcrumb/category links either.
- **Likely cause: MegaSuper appears to be migrating to Instaleap**, a LatAm quick-commerce SaaS platform — the homepage loads all banner images from `wanda-files.instaleap.io` and references a help page at `megasuper.instaleap.io`. This is consistent with the site-wide rendering changes since the original 2026-06-15 research; the storefront likely now hydrates its catalog from Instaleap's backend via client-side API calls rather than baking it into SSR HTML.
- **No public API endpoint found via static analysis.** Searched the homepage's Next.js RSC payload for `apiUrl`/`graphqlUrl`/`NEXT_PUBLIC_*` config keys and any `instaleap`-hosted API/graphql URLs — none found. A `site:megasuper.com /p/` Google search confirms individual product pages are indexed, but isn't a viable bulk-enumeration strategy.
- **Conclusion: needs a browser, not curl.** Finding the actual data-fetching mechanism (REST call, GraphQL query, or otherwise) requires inspecting real browser network traffic while the category grid loads — exactly the kind of investigation Playwright was already earmarked for (section 1's original decision deferred Playwright only for AutoMercado; MegaSuper has now joined it). `JsonLdScraper` is left registered — it fails gracefully (0 product URLs found, 0 products scraped, clearly visible in the new `StoreScrapeResult` summary) rather than crashing, so it's safe to leave running while this is deferred.
- **Next step when picked up:** open MegaSuper in a real browser with devtools open, watch the Network tab while browsing a category, identify the actual API call, then decide whether `JsonLdScraper` can keep using JSON-LD per-product (just with a new enumeration source) or whether the whole scraper should switch to consuming Instaleap's API directly (which may also return price/availability without needing the per-page HTML fetch at all).

---

## 8. Live Test Framework

**Date:** 2026-06-16
**Status:** Implemented — `LiveScraperSmokeTests`, covers VTEX (×3) + PriceSmart

### Why this exists

The section 7 incident happened because nobody actually ran a scraper against the real internet between writing it and triggering a 30-minute nightly job. A fast, real (non-mocked) smoke test closes that gap — it would have caught all three root-caused bugs in seconds instead of a 30-minute silent failure followed by a multi-hour forensic investigation.

### Design

- **Lives in the same test project** as the mocked unit tests (`scraper/tests/CanastaCR.Scraper.Tests/Live/LiveScraperSmokeTests.cs`), tagged `[Trait("Category", "Live")]`.
- **Excluded from the default run.** `scraper:test` (and the pre-commit hook, which calls it) now runs `dotnet test --filter "Category!=Live"` — these tests are slow and network-dependent, not appropriate for every commit.
- **Run on demand** via `.\scripts\run.ps1 scraper:test:live`, or whenever a store's site is suspected to have changed.
- **Capped at 25 products per store** via the `maxProducts` parameter (section 7's fix), so each store finishes in under a second (VTEX) to ~12 seconds (PriceSmart, which has to scan its global catalog client-side-filtering for Costa Rica — see section 7.1). The whole suite runs in well under 20 seconds, not the ~30 minutes a full nightly crawl takes.
- **Assertions per store:** at least 25 products returned; every product has a non-empty name and `Currency == "CRC"`; every *available* product has `Price > 0` (price == 0 on an available item is almost certainly a parsing bug, not real data); at least one product is available at all (catches a scraper that runs but marks everything unavailable). VTEX stores additionally assert >50% of products carry an EAN (catches a regression of the section 7 bug #1 fix). PriceSmart additionally asserts every barcode is `null` (since none exist there — see section 7.1; a non-null barcode showing up would mean something changed and needs investigation, not silent acceptance).
- **MegaSuper has no live test yet** — pointless until enumeration (section 7.2) is fixed; would just assert "0 products," which isn't a useful regression guard. Add one when that's unblocked.

### What it caught immediately

Running this suite for the first time (before any VTEX fix beyond the EAN correction) surfaced a *second*, previously-unknown VTEX bug: the scraper was recursing into ~860 leaf categories per store instead of ~26 top-level ones (VTEX's category filter already matches descendants), and most leaf categories turned out to be empty. Production-scale runs never made this obvious because a full catalog crawl eventually finds enough products regardless — wasted requests just looked like "the scraper takes a while." A tight, capped smoke test made the inefficiency impossible to miss: every VTEX store timed out at 60 seconds trying to reach 25 products. Fixed by switching to top-level category IDs; all three VTEX stores now return 25 valid products in under a second.

### Using this during development

Beyond pre-merge validation, this is the fastest way to iterate on scraper code: change a parser, run `scraper:test:live`, see real pass/fail against the actual site in seconds — no need to trigger a full Hangfire job and wait, then go check the database to see what (if anything) landed.

---

## 9. Observability & Operations

**Date:** 2026-06-16
**Status:** Implemented — local file logging, independent per-store jobs, verified end-to-end

This section exists because of the section 7 incident: a 30-minute run that silently wrote zero rows, diagnosable only by re-deriving everything from scratch since no log survived past the terminal session. Two changes close that gap: a persistent local log, and making each store's scrape a genuinely independent unit of work instead of one big sequential job.

### 9.1 Local log file (the "dump the process locally" piece)

The scraper now logs through Serilog to two sinks:
- **Console** — same as before, useful while watching `scraper:run` in a foreground terminal.
- **Rolling daily file** at `scraper/src/CanastaCR.Scraper/logs/scrape-{yyyyMMdd}.log` (already covered by the repo's `[Ll]ogs/` gitignore pattern — nothing to add there). Retains 14 days, then rolls off.

Critically, **Serilog reads its levels from the same `appsettings.json`/`appsettings.Development.json` `Logging:LogLevel` section every other part of this app already uses** (`builder.Host.UseSerilog((context, services, cfg) => cfg.ReadFrom.Configuration(context.Configuration)...)`), rather than hardcoding levels in `Program.cs`. The first version of this change hardcoded levels and silently ignored that config section entirely — editing `appsettings.json` would have done nothing, which is exactly the kind of trap that makes an incident *harder* to diagnose. Caught by actually running it and finding the log flooded with raw parameterized SQL from `Microsoft.EntityFrameworkCore.Database.Command` (logged at `Information`, which nothing was overriding). Fixed by adding `"Microsoft.EntityFrameworkCore": "Warning"` to `appsettings.json`'s `Logging:LogLevel`, now actually respected.

**How to use it:**
```powershell
.\scripts\run.ps1 scraper:logs:tail     # follow today's log file live (Get-Content -Wait)
```
Or open `scraper/src/CanastaCR.Scraper/logs/scrape-YYYYMMDD.log` directly for a past date.

**What gets logged:** job start/finish per store (`ScrapeStoreJob`), write counts (`PriceWriterService`: written/skipped/failed), fuzzy-match decisions (`ProductMatcherService`: auto-merge vs. ambiguous-match-creates-new, with the score), and any exception with full stack trace. Verified live (2026-06-16): triggering Walmart, MaxiPalí, Más x Menos, and PriceSmart simultaneously produced a clean, readable, interleaved log with no SQL noise.

### 9.2 Independent, concurrent per-store jobs

**Before:** `ScrapeAllStoresJob.RunAsync` looped through every matching `IStoreScraper` sequentially, in-process, inside one Hangfire job. Triggering "/api/scrape/vtex" ran Walmart, then MaxiPalí, then Más x Menos, one after another, all sharing one job's lifetime — Hangfire only ever showed one "ScrapeAllStoresJob" entry, succeeded or failed as a single unit, with no way to see one store's progress independent of the others.

**Now:**
- `ScrapeStoreJob` is the real unit of work — it scrapes exactly **one** store (selected by `StoreName`) and returns a `StoreScrapeResult`.
- `ScrapeAllStoresJob` is a thin **fan-out enqueuer** — for each scraper matching the platform filter (or all of them), it calls `IBackgroundJobClient.Enqueue<ScrapeStoreJob>(...)` and returns immediately with the list of newly-created job IDs. It never scrapes anything itself anymore.
- Each `ScrapeStoreJob` Hangfire execution gets its **own DI scope** (Hangfire.AspNetCore's job activator creates a new `IServiceScopeFactory.CreateScope()` per job), so each gets its own `ScraperDbContext`/`ProductMatcherService`/`PriceWriterService` instances. EF Core's `DbContext` is not thread-safe, so this matters — concurrent store jobs never share one, by construction, not by convention.
- Hangfire's default worker count (`BackgroundJobServerOptions.WorkerCount`, observed as 20 on this dev machine — roughly `Environment.ProcessorCount × 5`) already provides far more parallelism than the 4–5 stores this scrapes today need. No tuning required at this scale; revisit if the store count grows enough to saturate it.

**Trigger endpoints**, now precise:
| Endpoint | Behavior |
|---|---|
| `POST /api/scrape` | Fans out all registered stores as independent jobs |
| `POST /api/scrape/vtex` | Fans out the 3 VTEX stores (Walmart, MaxiPalí, Más x Menos) as independent jobs |
| `POST /api/scrape/walmart` `/maxipali` `/masxmenos` `/megasuper` `/pricesmart` | Enqueues a **single** `ScrapeStoreJob` directly, by exact store name — no platform-filter ambiguity. (Previously `/walmart` actually ran the same "scrape everything" job as every other endpoint — the platform name in the URL was cosmetic. Fixed alongside this change.) |

**Verified live (2026-06-16):** triggered Walmart, MaxiPalí, Más x Menos, and PriceSmart via four separate `POST` calls. Log timestamps confirm all four started within ~210ms of each other and ran concurrently — the three VTEX stores finished in ~1.5s while PriceSmart's longer full-catalog scan (its own ~7s) was still running, completing independently afterward without blocking or being blocked by the others. Each is now a separate row in the Hangfire dashboard's job list, individually showing its own `StoreScrapeResult` (written/skipped/failed) as that job's result.

### 9.3 Runbook: "what happened during/after a scrape run?"

1. **Check the Hangfire dashboard** at `/hangfire` (e.g. `http://localhost:5050/hangfire` locally). Each store now appears as its own job — check its state (Succeeded/Failed/Processing) and its **Result** (the `StoreScrapeResult`: scraped/written/skipped/failed counts). A `Succeeded` state with `Written: 0` is the section 7 incident's signature — investigate immediately, don't treat it as fine.
2. **Tail or open the log file** (`scraper:logs:tail`, or the dated file directly) for the detail behind those counts — which products were skipped and why, any exception with stack trace, fuzzy-match decisions.
3. **If a store looks broken, isolate it with the live test suite** (`scraper:test:live`, or just instantiate that one scraper) before assuming the bug is in the shared pipeline (matcher/writer) — section 7's incident had three *independent* root causes, one per platform; check each store in isolation rather than assuming one fix covers all of them.
4. **Verify against the actual database** via the main API (`GET /api/products`, `GET /api/products/{id}/prices`) — the Hangfire result and log file describe what the scraper *did*; checking the API confirms what's actually queryable afterward.
5. **`GET /api/scrape/status`** gives a quick aggregate (enqueued/processing/succeeded/failed counts across all jobs in storage) — useful for "is anything still running" but not a substitute for checking individual job results per store.

### 9.4 Azure logging — not yet applicable to the scraper

The existing `.\scripts\run.ps1 log:tail` (`az webapp log tail`) streams logs for the **main API**, which is deployed to Azure App Service. The scraper is **local-only** today (section 4's decision: start local, add an Azure Container Apps Job later). When that move happens, this section should be updated with the equivalent Azure-side command (likely `az containerapp logs show` or Azure Monitor/Log Analytics querying, depending on which Azure compute option is chosen) — until then, section 9.1–9.3 above is the complete operational story.

# Scraper API — Postman Endpoint List

Start the scraper locally first:
```
.\scripts\run.ps1 scraper:run
```
Service runs on `http://localhost:5050`.

All endpoints accept an optional `?maxProducts=N` query param to cap the scrape (e.g. for a quick test) instead of running the full catalog.

---

## Trigger endpoints

Each store now runs as its own independent Hangfire job — they can run concurrently and each shows up as its own row in the Hangfire dashboard with its own result (written/skipped/failed counts), not bundled into one shared job. See [ARCHITECTURE.md section 9](ARCHITECTURE.md#9-observability--operations) for details.

### Trigger all stores (fans out one independent job per store)
```
POST http://localhost:5050/api/scrape
POST http://localhost:5050/api/scrape?maxProducts=25
```
Returns 202 Accepted.

### Trigger all VTEX stores — Walmart + MaxiPalí + Más x Menos (3 independent jobs)
```
POST http://localhost:5050/api/scrape/vtex
```
Returns 202 Accepted.

### Trigger a single store (exact, no platform ambiguity)
```
POST http://localhost:5050/api/scrape/walmart
POST http://localhost:5050/api/scrape/maxipali
POST http://localhost:5050/api/scrape/masxmenos
POST http://localhost:5050/api/scrape/megasuper
POST http://localhost:5050/api/scrape/pricesmart
```
Returns 202 Accepted. MegaSuper currently scrapes 0 products (enumeration broken — see ARCHITECTURE.md section 7.2); the job still completes cleanly, it just reports 0 written.

---

## Status

### Job queue status (aggregate across all jobs)
```
GET http://localhost:5050/api/scrape/status
```
Returns:
```json
{
  "enqueued": 0,
  "processing": 1,
  "succeeded": 3,
  "failed": 0,
  "scheduled": 0
}
```
For per-store detail, use the Hangfire dashboard instead — this endpoint only gives a coarse aggregate.

---

## Hangfire Dashboard
```
GET http://localhost:5050/hangfire
```
Open in browser — shows job history, each store's individual result, retries, and the recurring job schedule (nightly at 4 AM CR time, fans out to all stores).

---

## Local logs

```powershell
.\scripts\run.ps1 scraper:logs:tail
```
Follows today's log file live — same business-relevant detail as the console (job start/finish per store, write counts, fuzzy-match decisions, exceptions), persisted to `scraper/src/CanastaCR.Scraper/logs/` so it survives after the terminal closes.

---

## Verify results via main API

After a scrape completes, check the results through the main API (requires `api` running on port 7068):

```
GET https://localhost:7068/api/products?pageSize=50
```
Products scraped will appear here with their price reports.

```
GET https://localhost:7068/api/products/{productId}/prices
```
Price comparison across all stores for a given product.

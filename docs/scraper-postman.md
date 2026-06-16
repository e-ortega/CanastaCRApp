# Scraper API — Postman Endpoint List

Start the scraper locally first:
```
.\scripts\run.ps1 scraper:run
```
Service runs on `http://localhost:5050`.

---

## Trigger endpoints

### Trigger all stores
```
POST http://localhost:5050/api/scrape
```
Returns 202 Accepted. Enqueues all scrapers (MaxiPalí, Más x Menos, MegaSuper, PriceSmart) as a Hangfire background job.

### Trigger VTEX only (Walmart + MaxiPalí + Más x Menos)
```
POST http://localhost:5050/api/scrape/vtex
```
Returns 202 Accepted.

### Trigger Walmart only
```
POST http://localhost:5050/api/scrape/walmart
```
Returns 202 Accepted.

### Trigger MegaSuper only
```
POST http://localhost:5050/api/scrape/megasuper
```
Returns 202 Accepted.

### Trigger PriceSmart only
```
POST http://localhost:5050/api/scrape/pricesmart
```
Returns 202 Accepted.

---

## Status

### Job queue status
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

---

## Hangfire Dashboard
```
GET http://localhost:5050/hangfire
```
Open in browser — shows job history, retries, recurring job schedule (nightly at 4 AM CR time).

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

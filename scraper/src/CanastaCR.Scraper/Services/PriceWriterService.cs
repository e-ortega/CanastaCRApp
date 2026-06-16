using CanastaCR.Core.Entities;
using CanastaCR.Core.Enums;
using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Persistence;

namespace CanastaCR.Scraper.Services;

public class PriceWriterService(
    ScraperDbContext db,
    ProductMatcherService matcher,
    ILogger<PriceWriterService> logger) : IScrapeResultStore
{
    private static readonly TimeSpan PriceExpiry = TimeSpan.FromDays(3);

    public async Task<WriteResult> WriteAsync(StoreChain chain, IReadOnlyList<ScrapedProduct> products, CancellationToken ct = default)
    {
        var storeName = chain.GetDisplayName();
        var written = 0;
        var skipped = 0;
        var failed = 0;
        var writtenSinceLastFlush = 0; // entities currently pending in an uncommitted SaveChanges batch
        var now = DateTimeOffset.UtcNow;

        foreach (var scraped in products)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var product = await matcher.FindOrCreateProductAsync(scraped, ct);

                // Skip unavailable products but still record them so we know price dropped/gone
                if (!scraped.IsAvailable)
                {
                    logger.LogDebug("Product '{Name}' unavailable at {Store} — skipping price report", scraped.Name, storeName);
                    skipped++;
                    continue;
                }

                // Chain-level price — every chain scraped so far sets one nationwide price, not
                // a per-location price (see docs/ARCHITECTURE.md section 11), so this is never
                // tied to one specific Store row.
                var report = new PriceReport
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    StoreId = null,
                    Chain = chain,
                    Price = scraped.Price,
                    Currency = scraped.Currency,
                    Source = PriceSource.Scraped,
                    ReportedAt = now,
                    ExpiresAt = now.Add(PriceExpiry)
                };

                db.PriceReports.Add(report);
                written++;
                writtenSinceLastFlush++;

                // Flush every 100 records to avoid huge in-memory change tracker
                if (writtenSinceLastFlush >= 100)
                {
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("{Store}: saved {Count} price reports so far", storeName, written);
                    writtenSinceLastFlush = 0;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write price for '{Name}' at {Store}", scraped.Name, storeName);
                failed++;

                // SaveChanges is one transaction — if it just threw, nothing pending in this
                // batch actually landed, so reclassify it as failed rather than (incorrectly)
                // counted written. Then clear the tracker so the failure can't poison every
                // subsequent save for the rest of this store's run: confirmed live 2026-06-16,
                // an unhandled duplicate-key race (two stores concurrently inserting the same
                // EAN) left a half-saved entity in the tracker, and every later SaveChanges on
                // this same DbContext kept re-throwing the identical error — 10,956 times in
                // one incident — until the entire store's writes for that run were lost.
                if (writtenSinceLastFlush > 0)
                {
                    failed += writtenSinceLastFlush;
                    written -= writtenSinceLastFlush;
                    writtenSinceLastFlush = 0;
                }
                db.ChangeTracker.Clear();
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Store}: final flush of {Count} pending price reports failed", storeName, writtenSinceLastFlush);
            failed += writtenSinceLastFlush;
            written -= writtenSinceLastFlush;
            writtenSinceLastFlush = 0;
        }

        logger.LogInformation("{Store}: finished — {Written} written, {Skipped} skipped, {Failed} failed (of {Total})",
            storeName, written, skipped, failed, products.Count);

        return new WriteResult(written, skipped, failed);
    }
}

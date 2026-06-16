using CanastaCR.Scraper.Abstractions;

namespace CanastaCR.Scraper.Jobs;

public record StoreScrapeResult(string StoreName, int Scraped, int Written, int Skipped, int Failed, string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>
/// Scrapes exactly one store. This is the actual unit of work Hangfire schedules — each
/// instance runs as its own independent job (own job ID, own status, own result on the
/// dashboard), so multiple stores can run concurrently and be tracked separately instead of
/// all sharing one job's lifetime. See ScrapeAllStoresJob, which fans out one of these per
/// matching scraper instead of looping through them in-process.
/// </summary>
public class ScrapeStoreJob(
    IEnumerable<IStoreScraper> scrapers,
    IScrapeResultStore resultStore,
    ILogger<ScrapeStoreJob> logger)
{
    public async Task<StoreScrapeResult> RunAsync(string storeName, int? maxProducts = null, CancellationToken ct = default)
    {
        var scraper = scrapers.FirstOrDefault(s => s.StoreName == storeName);
        if (scraper is null)
        {
            logger.LogWarning("No scraper registered for store '{StoreName}'", storeName);
            return new StoreScrapeResult(storeName, 0, 0, 0, 0, Error: "No scraper registered for this store name");
        }

        logger.LogInformation("{Store}: scrape job starting", storeName);
        try
        {
            var products = await scraper.ScrapeAsync(maxProducts, ct);
            var result = await resultStore.WriteAsync(scraper.StoreName, products, ct);

            logger.LogInformation("{Store}: finished — {Written} written, {Skipped} skipped, {Failed} failed (of {Total} scraped)",
                storeName, result.Written, result.Skipped, result.Failed, products.Count);

            return new StoreScrapeResult(storeName, products.Count, result.Written, result.Skipped, result.Failed, Error: null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Store}: scrape job failed", storeName);
            return new StoreScrapeResult(storeName, 0, 0, 0, 0, Error: ex.Message);
        }
    }
}

using CanastaCR.Core.Enums;
using CanastaCR.Scraper.Abstractions;

namespace CanastaCR.Scraper.Jobs;

public record StoreScrapeResult(StoreChain Chain, int Scraped, int Written, int Skipped, int Failed, string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>
/// Scrapes exactly one chain. This is the actual unit of work Hangfire schedules — each
/// instance runs as its own independent job (own job ID, own status, own result on the
/// dashboard), so multiple chains can run concurrently and be tracked separately instead of
/// all sharing one job's lifetime. See ScrapeAllStoresJob, which fans out one of these per
/// matching scraper instead of looping through them in-process.
/// </summary>
public class ScrapeStoreJob(
    IEnumerable<IStoreScraper> scrapers,
    IScrapeResultStore resultStore,
    ILogger<ScrapeStoreJob> logger)
{
    public async Task<StoreScrapeResult> RunAsync(StoreChain chain, int? maxProducts = null, CancellationToken ct = default)
    {
        var scraper = scrapers.FirstOrDefault(s => s.Chain == chain);
        if (scraper is null)
        {
            logger.LogWarning("No scraper registered for chain '{Chain}'", chain);
            return new StoreScrapeResult(chain, 0, 0, 0, 0, Error: "No scraper registered for this chain");
        }

        logger.LogInformation("{Store}: scrape job starting", scraper.StoreName);
        try
        {
            var products = await scraper.ScrapeAsync(maxProducts, ct);
            var result = await resultStore.WriteAsync(chain, products, ct);

            logger.LogInformation("{Store}: finished — {Written} written, {Skipped} skipped, {Failed} failed (of {Total} scraped)",
                scraper.StoreName, result.Written, result.Skipped, result.Failed, products.Count);

            return new StoreScrapeResult(chain, products.Count, result.Written, result.Skipped, result.Failed, Error: null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Store}: scrape job failed", scraper.StoreName);
            return new StoreScrapeResult(chain, 0, 0, 0, 0, Error: ex.Message);
        }
    }
}

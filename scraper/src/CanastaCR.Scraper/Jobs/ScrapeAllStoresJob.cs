using CanastaCR.Scraper.Abstractions;

namespace CanastaCR.Scraper.Jobs;

public class ScrapeAllStoresJob(
    IEnumerable<IStoreScraper> scrapers,
    IScrapeResultStore resultStore,
    ILogger<ScrapeAllStoresJob> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Scrape job started — {Count} scrapers registered", scrapers.Count());

        foreach (var scraper in scrapers)
        {
            if (ct.IsCancellationRequested) break;

            logger.LogInformation("Starting scraper: {Store}", scraper.StoreName);
            try
            {
                var products = await scraper.ScrapeAsync(ct);
                await resultStore.WriteAsync(scraper.StoreName, products, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scraper '{Store}' failed", scraper.StoreName);
            }
        }

        logger.LogInformation("Scrape job completed");
    }
}

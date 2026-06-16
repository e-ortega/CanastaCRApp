using CanastaCR.Scraper.Abstractions;

namespace CanastaCR.Scraper.Jobs;

public record StoreScrapeResult(string StoreName, int Scraped, int Written, int Skipped, int Failed, string? Error)
{
    public bool Succeeded => Error is null;
}

public class ScrapeAllStoresJob(
    IEnumerable<IStoreScraper> scrapers,
    IScrapeResultStore resultStore,
    ILogger<ScrapeAllStoresJob> logger)
{
    /// <summary>
    /// Returns a per-store summary — Hangfire displays the return value on the job detail page,
    /// so "Succeeded" now means something concrete instead of just "no exception escaped".
    /// </summary>
    /// <param name="maxProductsPerStore">Caps each store's scrape — null means full catalog (real nightly run).</param>
    /// <param name="platform">When set, only runs scrapers whose Platform matches (e.g. "vtex", "megasuper", "pricesmart").</param>
    public async Task<IReadOnlyList<StoreScrapeResult>> RunAsync(
        int? maxProductsPerStore = null, string? platform = null, CancellationToken ct = default)
    {
        var selected = platform is null
            ? scrapers
            : scrapers.Where(s => string.Equals(s.Platform, platform, StringComparison.OrdinalIgnoreCase));

        var summaries = new List<StoreScrapeResult>();
        var selectedList = selected.ToList();
        logger.LogInformation("Scrape job started — {Count} scrapers selected (platform filter: {Platform})",
            selectedList.Count, platform ?? "none");

        foreach (var scraper in selectedList)
        {
            if (ct.IsCancellationRequested) break;

            logger.LogInformation("Starting scraper: {Store}", scraper.StoreName);
            try
            {
                var products = await scraper.ScrapeAsync(maxProductsPerStore, ct);
                var result = await resultStore.WriteAsync(scraper.StoreName, products, ct);

                summaries.Add(new StoreScrapeResult(
                    scraper.StoreName, products.Count, result.Written, result.Skipped, result.Failed, Error: null));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scraper '{Store}' failed", scraper.StoreName);
                summaries.Add(new StoreScrapeResult(scraper.StoreName, 0, 0, 0, 0, Error: ex.Message));
            }
        }

        logger.LogInformation("Scrape job completed: {Summary}",
            string.Join(", ", summaries.Select(s => $"{s.StoreName}={s.Written}w/{s.Failed}f")));

        return summaries;
    }
}

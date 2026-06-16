using CanastaCR.Scraper.Abstractions;
using Hangfire;

namespace CanastaCR.Scraper.Jobs;

/// <summary>
/// Fans out one independent ScrapeStoreJob per matching scraper instead of scraping them
/// sequentially in-process. Each store job runs and is tracked completely independently in
/// the Hangfire dashboard (own status, own duration, own StoreScrapeResult) and can execute
/// concurrently with the others, bounded only by the configured Hangfire worker count
/// (BackgroundJobServerOptions.WorkerCount, default ~Environment.ProcessorCount * 5 — plenty
/// of headroom for the handful of stores this scrapes today).
///
/// This itself runs fast — it only enqueues, it never awaits a scrape — so triggering it
/// (manually or via the nightly cron) returns almost immediately; the real work happens in
/// the independently-scheduled ScrapeStoreJob instances.
/// </summary>
public class ScrapeAllStoresJob(
    IEnumerable<IStoreScraper> scrapers,
    IBackgroundJobClient bgJobClient,
    ILogger<ScrapeAllStoresJob> logger)
{
    /// <param name="maxProductsPerStore">Caps each store's scrape — null means full catalog (real nightly run).</param>
    /// <param name="platform">When set, only fans out scrapers whose Platform matches (e.g. "vtex", "megasuper", "pricesmart").</param>
    /// <returns>The Hangfire job IDs created — one per store fanned out to.</returns>
    public IReadOnlyList<string> RunAsync(int? maxProductsPerStore = null, string? platform = null, CancellationToken ct = default)
    {
        var selected = (platform is null
            ? scrapers
            : scrapers.Where(s => string.Equals(s.Platform, platform, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        logger.LogInformation("Fanning out {Count} independent store jobs (platform filter: {Platform})",
            selected.Count, platform ?? "none");

        var jobIds = selected
            .Select(s => bgJobClient.Enqueue<ScrapeStoreJob>(j => j.RunAsync(s.Chain, maxProductsPerStore, CancellationToken.None)))
            .ToList();

        logger.LogInformation("Fanned out jobs: {JobIds}", string.Join(", ", jobIds));
        return jobIds;
    }
}

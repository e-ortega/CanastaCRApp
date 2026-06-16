using CanastaCR.Core.Enums;

namespace CanastaCR.Scraper.Abstractions;

public interface IStoreScraper
{
    /// <summary>
    /// The chain this scraper writes prices for — the canonical identity used to route jobs
    /// and write PriceReports. Not tied to one physical location: every chain scraped so far
    /// sets one nationwide price, not a per-location price (see docs/ARCHITECTURE.md section
    /// 11), so scraped prices are chain-level, never a specific Store row.
    /// </summary>
    StoreChain Chain { get; }

    /// <summary>Friendly label for logs — e.g. "MaxiPalí", not tied to one specific location.</summary>
    string StoreName { get; }

    /// <summary>
    /// Stable platform tag (e.g. "vtex", "megasuper", "pricesmart") used to filter which
    /// scrapers run for a given trigger — distinct from StoreName, which has accents/spaces
    /// and isn't a reliable filter key.
    /// </summary>
    string Platform { get; }

    /// <param name="maxProducts">
    /// Stop once this many products have been collected, instead of scraping the full catalog.
    /// Null (default) means no limit — used for real nightly runs. A small value lets manual
    /// testing and the live smoke-test suite validate a store in seconds instead of minutes.
    /// </param>
    Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(int? maxProducts = null, CancellationToken ct = default);
}

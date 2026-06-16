namespace CanastaCR.Scraper.Abstractions;

public interface IStoreScraper
{
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

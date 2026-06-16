namespace CanastaCR.Scraper.Abstractions;

public interface IStoreScraper
{
    string StoreName { get; }
    Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken ct = default);
}

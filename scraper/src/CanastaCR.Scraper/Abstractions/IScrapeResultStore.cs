namespace CanastaCR.Scraper.Abstractions;

public interface IScrapeResultStore
{
    Task WriteAsync(string storeName, IReadOnlyList<ScrapedProduct> products, CancellationToken ct = default);
}

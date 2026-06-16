namespace CanastaCR.Scraper.Abstractions;

public record WriteResult(int Written, int Skipped, int Failed)
{
    public int Total => Written + Skipped + Failed;
}

public interface IScrapeResultStore
{
    Task<WriteResult> WriteAsync(string storeName, IReadOnlyList<ScrapedProduct> products, CancellationToken ct = default);
}

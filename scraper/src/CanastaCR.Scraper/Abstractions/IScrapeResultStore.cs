using CanastaCR.Core.Enums;

namespace CanastaCR.Scraper.Abstractions;

public record WriteResult(int Written, int Skipped, int Failed)
{
    public int Total => Written + Skipped + Failed;
}

public interface IScrapeResultStore
{
    Task<WriteResult> WriteAsync(StoreChain chain, IReadOnlyList<ScrapedProduct> products, CancellationToken ct = default);
}

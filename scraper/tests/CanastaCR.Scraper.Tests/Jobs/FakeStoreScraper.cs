using CanastaCR.Scraper.Abstractions;

namespace CanastaCR.Scraper.Tests.Jobs;

internal class FakeStoreScraper(string storeName, string platform, IReadOnlyList<ScrapedProduct>? products = null) : IStoreScraper
{
    public string StoreName => storeName;
    public string Platform => platform;

    public Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(int? maxProducts = null, CancellationToken ct = default) =>
        Task.FromResult(products ?? []);
}

internal class ThrowingStoreScraper(string storeName) : IStoreScraper
{
    public string StoreName => storeName;
    public string Platform => "fake";

    public Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(int? maxProducts = null, CancellationToken ct = default) =>
        throw new InvalidOperationException("Simulated scrape failure");
}

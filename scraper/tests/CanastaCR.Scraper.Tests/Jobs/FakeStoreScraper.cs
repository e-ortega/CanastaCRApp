using CanastaCR.Core.Enums;
using CanastaCR.Scraper.Abstractions;

namespace CanastaCR.Scraper.Tests.Jobs;

internal class FakeStoreScraper(StoreChain chain, string platform, IReadOnlyList<ScrapedProduct>? products = null) : IStoreScraper
{
    public StoreChain Chain => chain;
    public string StoreName => chain.GetDisplayName();
    public string Platform => platform;

    public Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(int? maxProducts = null, CancellationToken ct = default) =>
        Task.FromResult(products ?? []);
}

internal class ThrowingStoreScraper(StoreChain chain) : IStoreScraper
{
    public StoreChain Chain => chain;
    public string StoreName => chain.GetDisplayName();
    public string Platform => "fake";

    public Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(int? maxProducts = null, CancellationToken ct = default) =>
        throw new InvalidOperationException("Simulated scrape failure");
}

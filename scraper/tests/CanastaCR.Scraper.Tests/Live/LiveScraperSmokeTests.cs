using CanastaCR.Core.Enums;
using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Scrapers;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanastaCR.Scraper.Tests.Live;

/// <summary>
/// Hits the REAL store websites — no mocking. Excluded from the default "dotnet test" run
/// (and the pre-commit hook) via the "Category=Live" trait, since these are slow, network-
/// dependent, and not appropriate to run on every commit. Run explicitly with
/// `.\scripts\run.ps1 scraper:test:live` before trusting a scraper change, or after a store's
/// website changes and you suspect something broke (this is exactly the kind of incident that
/// happened on 2026-06-16 — see docs/ARCHITECTURE.md section 7 — that a quick live check would
/// have caught immediately instead of a 30-minute silent failure).
///
/// Each test caps at MinProductsExpected via the maxProducts parameter, so this suite finishes
/// in seconds per store rather than running a full multi-minute catalog crawl.
/// </summary>
[Trait("Category", "Live")]
public class LiveScraperSmokeTests
{
    private const int MinProductsExpected = 25;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public Task MaxiPali_ReturnsAtLeast25ValidProducts() =>
        AssertVtexStoreWorks(StoreChain.MaxiPali, "https://www.maxipali.co.cr");

    [Fact]
    public Task MasXMenos_ReturnsAtLeast25ValidProducts() =>
        AssertVtexStoreWorks(StoreChain.MasXMenos, "https://www.masxmenos.cr");

    [Fact]
    public Task Walmart_ReturnsAtLeast25ValidProducts() =>
        AssertVtexStoreWorks(StoreChain.Walmart, "https://www.walmart.co.cr");

    [Fact]
    public async Task PriceSmart_ReturnsAtLeast25ValidProducts()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        using var http = NewHttpClient();
        var scraper = new CommerceToolsScraper(http, NullLogger<CommerceToolsScraper>.Instance);

        var results = await scraper.ScrapeAsync(MinProductsExpected, cts.Token);

        AssertValidProducts(results, "PriceSmart");
        // PriceSmart has no EAN/UPC field at all (confirmed in ARCHITECTURE.md section 7.1) —
        // every product should have a null barcode, never a false one.
        Assert.All(results, p => Assert.Null(p.Barcode));
    }

    private static async Task AssertVtexStoreWorks(StoreChain chain, string baseUrl)
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        using var http = NewHttpClient();
        var scraper = new VtexScraper(chain, baseUrl, http, NullLogger<VtexScraper>.Instance);
        var storeName = chain.GetDisplayName();

        var results = await scraper.ScrapeAsync(MinProductsExpected, cts.Token);

        AssertValidProducts(results, storeName);

        var barcodeRate = results.Count(p => !string.IsNullOrEmpty(p.Barcode)) / (double)results.Count;
        Assert.True(barcodeRate > 0.5,
            $"{storeName}: expected most VTEX products to carry an EAN, got {barcodeRate:P0} ({results.Count(p => !string.IsNullOrEmpty(p.Barcode))}/{results.Count})");
    }

    private static void AssertValidProducts(IReadOnlyList<ScrapedProduct> results, string storeName)
    {
        Assert.True(results.Count >= MinProductsExpected,
            $"{storeName}: expected >= {MinProductsExpected} products, got {results.Count}");

        foreach (var p in results)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name), $"{storeName}: product with empty name (url: {p.SourceUrl})");
            Assert.Equal("CRC", p.Currency);

            if (p.IsAvailable)
                Assert.True(p.Price > 0, $"{storeName}: available product '{p.Name}' has price <= 0 — likely a parsing bug");
        }

        Assert.Contains(results, p => p.IsAvailable);
    }

    private static HttpClient NewHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Add("User-Agent", "CanastaCR-Scraper-LiveTest/1.0 (price comparison research)");
        return http;
    }
}

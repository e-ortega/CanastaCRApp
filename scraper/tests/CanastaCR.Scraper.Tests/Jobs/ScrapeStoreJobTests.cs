using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CanastaCR.Scraper.Tests.Jobs;

public class ScrapeStoreJobTests
{
    [Fact]
    public async Task RunAsync_FindsScraperByName_AndReturnsWriteCounts()
    {
        var product = new ScrapedProduct("Test Product", "Brand", "123", 100m, "CRC", null, null, true, "https://example.com");
        var scraper = new FakeStoreScraper("StoreA", "vtex", [product]);

        var mockStore = new Mock<IScrapeResultStore>();
        mockStore.Setup(s => s.WriteAsync("StoreA", It.IsAny<IReadOnlyList<ScrapedProduct>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WriteResult(Written: 1, Skipped: 0, Failed: 0));

        var sut = new ScrapeStoreJob([scraper], mockStore.Object, NullLogger<ScrapeStoreJob>.Instance);

        var result = await sut.RunAsync("StoreA", maxProducts: null, ct: CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("StoreA", result.StoreName);
        Assert.Equal(1, result.Scraped);
        Assert.Equal(1, result.Written);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenStoreNameNotRegistered()
    {
        var sut = new ScrapeStoreJob([], Mock.Of<IScrapeResultStore>(), NullLogger<ScrapeStoreJob>.Instance);

        var result = await sut.RunAsync("Unknown Store", maxProducts: null, ct: CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal(0, result.Written);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenScraperThrows()
    {
        var scraper = new ThrowingStoreScraper("StoreA");
        var sut = new ScrapeStoreJob([scraper], Mock.Of<IScrapeResultStore>(), NullLogger<ScrapeStoreJob>.Instance);

        var result = await sut.RunAsync("StoreA", maxProducts: null, ct: CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Simulated scrape failure", result.Error);
    }

    [Fact]
    public async Task RunAsync_PassesMaxProducts_ToTheSelectedScraperOnly()
    {
        var scraperA = new FakeStoreScraper("StoreA", "vtex");
        var scraperB = new FakeStoreScraper("StoreB", "vtex");
        var mockStore = new Mock<IScrapeResultStore>();
        mockStore.Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ScrapedProduct>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WriteResult(0, 0, 0));

        var sut = new ScrapeStoreJob([scraperA, scraperB], mockStore.Object, NullLogger<ScrapeStoreJob>.Instance);

        var result = await sut.RunAsync("StoreB", maxProducts: 5, ct: CancellationToken.None);

        Assert.Equal("StoreB", result.StoreName);
        mockStore.Verify(s => s.WriteAsync("StoreB", It.IsAny<IReadOnlyList<ScrapedProduct>>(), It.IsAny<CancellationToken>()), Times.Once);
        mockStore.Verify(s => s.WriteAsync("StoreA", It.IsAny<IReadOnlyList<ScrapedProduct>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

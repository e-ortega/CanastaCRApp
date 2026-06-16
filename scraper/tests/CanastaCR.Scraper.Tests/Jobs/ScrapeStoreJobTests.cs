using CanastaCR.Core.Enums;
using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CanastaCR.Scraper.Tests.Jobs;

public class ScrapeStoreJobTests
{
    [Fact]
    public async Task RunAsync_FindsScraperByChain_AndReturnsWriteCounts()
    {
        var product = new ScrapedProduct("Test Product", "Brand", "123", 100m, "CRC", null, null, true, "https://example.com");
        var scraper = new FakeStoreScraper(StoreChain.MaxiPali, "vtex", [product]);

        var mockStore = new Mock<IScrapeResultStore>();
        mockStore.Setup(s => s.WriteAsync(StoreChain.MaxiPali, It.IsAny<IReadOnlyList<ScrapedProduct>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WriteResult(Written: 1, Skipped: 0, Failed: 0));

        var sut = new ScrapeStoreJob([scraper], mockStore.Object, NullLogger<ScrapeStoreJob>.Instance);

        var result = await sut.RunAsync(StoreChain.MaxiPali, maxProducts: null, ct: CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(StoreChain.MaxiPali, result.Chain);
        Assert.Equal(1, result.Scraped);
        Assert.Equal(1, result.Written);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenChainNotRegistered()
    {
        var sut = new ScrapeStoreJob([], Mock.Of<IScrapeResultStore>(), NullLogger<ScrapeStoreJob>.Instance);

        var result = await sut.RunAsync(StoreChain.AutoMercado, maxProducts: null, ct: CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal(0, result.Written);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenScraperThrows()
    {
        var scraper = new ThrowingStoreScraper(StoreChain.MaxiPali);
        var sut = new ScrapeStoreJob([scraper], Mock.Of<IScrapeResultStore>(), NullLogger<ScrapeStoreJob>.Instance);

        var result = await sut.RunAsync(StoreChain.MaxiPali, maxProducts: null, ct: CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Simulated scrape failure", result.Error);
    }

    [Fact]
    public async Task RunAsync_PassesMaxProducts_ToTheSelectedScraperOnly()
    {
        var scraperA = new FakeStoreScraper(StoreChain.MaxiPali, "vtex");
        var scraperB = new FakeStoreScraper(StoreChain.MasXMenos, "vtex");
        var mockStore = new Mock<IScrapeResultStore>();
        mockStore.Setup(s => s.WriteAsync(It.IsAny<StoreChain>(), It.IsAny<IReadOnlyList<ScrapedProduct>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WriteResult(0, 0, 0));

        var sut = new ScrapeStoreJob([scraperA, scraperB], mockStore.Object, NullLogger<ScrapeStoreJob>.Instance);

        var result = await sut.RunAsync(StoreChain.MasXMenos, maxProducts: 5, ct: CancellationToken.None);

        Assert.Equal(StoreChain.MasXMenos, result.Chain);
        mockStore.Verify(s => s.WriteAsync(StoreChain.MasXMenos, It.IsAny<IReadOnlyList<ScrapedProduct>>(), It.IsAny<CancellationToken>()), Times.Once);
        mockStore.Verify(s => s.WriteAsync(StoreChain.MaxiPali, It.IsAny<IReadOnlyList<ScrapedProduct>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

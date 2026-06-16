using CanastaCR.Scraper.Abstractions;
using CanastaCR.Scraper.Jobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CanastaCR.Scraper.Tests.Jobs;

public class ScrapeAllStoresJobTests
{
    private static Mock<IBackgroundJobClient> NewMockClient(List<Job> createdJobs)
    {
        var mock = new Mock<IBackgroundJobClient>();
        mock.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, _) => createdJobs.Add(job))
            .Returns(() => Guid.NewGuid().ToString());
        return mock;
    }

    [Fact]
    public void RunAsync_EnqueuesOneIndependentJob_PerScraper_WhenNoPlatformFilter()
    {
        IStoreScraper[] scrapers =
        [
            new FakeStoreScraper("StoreA", "vtex"),
            new FakeStoreScraper("StoreB", "vtex"),
            new FakeStoreScraper("StoreC", "pricesmart")
        ];
        var createdJobs = new List<Job>();
        var mockClient = NewMockClient(createdJobs);

        var sut = new ScrapeAllStoresJob(scrapers, mockClient.Object, NullLogger<ScrapeAllStoresJob>.Instance);

        var jobIds = sut.RunAsync(maxProductsPerStore: 10, platform: null, ct: CancellationToken.None);

        Assert.Equal(3, jobIds.Count);
        Assert.Equal(3, createdJobs.Count);
        Assert.All(createdJobs, j => Assert.Equal(typeof(ScrapeStoreJob), j.Type));
    }

    [Fact]
    public void RunAsync_FiltersByPlatform_EnqueuingOnlyMatchingScrapers()
    {
        IStoreScraper[] scrapers =
        [
            new FakeStoreScraper("StoreA", "vtex"),
            new FakeStoreScraper("StoreB", "vtex"),
            new FakeStoreScraper("StoreC", "pricesmart")
        ];
        var createdJobs = new List<Job>();
        var mockClient = NewMockClient(createdJobs);

        var sut = new ScrapeAllStoresJob(scrapers, mockClient.Object, NullLogger<ScrapeAllStoresJob>.Instance);

        var jobIds = sut.RunAsync(maxProductsPerStore: null, platform: "vtex", ct: CancellationToken.None);

        Assert.Equal(2, jobIds.Count);
        Assert.Equal(2, createdJobs.Count);

        var targetedStoreNames = createdJobs.Select(j => (string)j.Args[0]).ToList();
        Assert.Contains("StoreA", targetedStoreNames);
        Assert.Contains("StoreB", targetedStoreNames);
        Assert.DoesNotContain("StoreC", targetedStoreNames);
    }

    [Fact]
    public void RunAsync_EnqueuesNothing_WhenPlatformFilterMatchesNoScrapers()
    {
        IStoreScraper[] scrapers = [new FakeStoreScraper("StoreA", "vtex")];
        var createdJobs = new List<Job>();
        var mockClient = NewMockClient(createdJobs);

        var sut = new ScrapeAllStoresJob(scrapers, mockClient.Object, NullLogger<ScrapeAllStoresJob>.Instance);

        var jobIds = sut.RunAsync(maxProductsPerStore: null, platform: "megasuper", ct: CancellationToken.None);

        Assert.Empty(jobIds);
        Assert.Empty(createdJobs);
    }
}

using CanastaCR.Scraper.Scrapers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CanastaCR.Scraper.Tests.Scrapers;

public class CommerceToolsScraperTests
{
    [Fact]
    public async Task ScrapeAsync_ParsesProducts_WhenApiRespondsSuccessfully()
    {
        var page1 = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new
                {
                    name = new { es = "Aceite Girasol 5L" },
                    slug = new { es = "aceite-girasol-5l-167401" },
                    masterVariant = new
                    {
                        prices = new[] { new { value = new { centAmount = 250000 } } },
                        images = new[] { new { url = "https://img.pricesmart.com/img.jpg" } },
                        availability = new { isOnStock = true },
                        attributes = Array.Empty<object>()
                    }
                }
            }
        });

        // Second page returns empty results to stop pagination
        var page2 = JsonSerializer.Serialize(new { results = Array.Empty<object>() });

        var callCount = 0;
        var handler = new CallCountingHandler(request =>
        {
            callCount++;
            var body = callCount == 1 ? page1 : page2;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });

        var http = new HttpClient(handler);
        var scraper = new CommerceToolsScraper(http, NullLogger<CommerceToolsScraper>.Instance);

        var results = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Aceite Girasol 5L", results[0].Name);
        Assert.Equal(2500m, results[0].Price);
        Assert.Equal("CRC", results[0].Currency);
        Assert.True(results[0].IsAvailable);
    }

    [Fact]
    public async Task ScrapeAsync_ReturnsEmpty_WhenApiFails()
    {
        var handler = new CallCountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(handler);
        var scraper = new CommerceToolsScraper(http, NullLogger<CommerceToolsScraper>.Instance);

        var results = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.Empty(results);
    }
}

internal class CallCountingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(handler(request));
}

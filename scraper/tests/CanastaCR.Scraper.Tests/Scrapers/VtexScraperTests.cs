using CanastaCR.Scraper.Scrapers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CanastaCR.Scraper.Tests.Scrapers;

public class VtexScraperTests
{
    [Fact]
    public async Task ScrapeAsync_ReturnsProducts_WhenApiRespondsSuccessfully()
    {
        var categoryTree = JsonSerializer.Serialize(new[]
        {
            new { id = 1, name = "Bebidas", children = Array.Empty<object>() }
        });

        var productPage = JsonSerializer.Serialize(new[]
        {
            new
            {
                productName = "Leche Dos Pinos 1L",
                brand = "Dos Pinos",
                ean = "7441044301002",
                link = "https://www.maxipali.co.cr/leche",
                categoryId = "1",
                items = new[]
                {
                    new
                    {
                        images = new[] { new { imageUrl = "https://example.com/img.jpg" } },
                        sellers = new[]
                        {
                            new { commertialOffer = new { Price = 1200m, IsAvailable = true } }
                        }
                    }
                }
            }
        });

        var handler = new FakeHttpMessageHandler(new Dictionary<string, string>
        {
            ["/api/catalog_system/pub/category/tree/3"] = categoryTree,
            ["/api/catalog_system/pub/products/search"] = productPage
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.maxipali.co.cr") };
        var scraper = new VtexScraper("MaxiPalí Alajuela", "https://www.maxipali.co.cr", http, NullLogger<VtexScraper>.Instance);

        var results = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Leche Dos Pinos 1L", results[0].Name);
        Assert.Equal("Dos Pinos", results[0].Brand);
        Assert.Equal("7441044301002", results[0].Barcode);
        Assert.Equal(1200m, results[0].Price);
        Assert.Equal("CRC", results[0].Currency);
        Assert.True(results[0].IsAvailable);
    }

    [Fact]
    public async Task ScrapeAsync_ReturnsEmpty_WhenCategoryTreeFails()
    {
        var handler = new FakeHttpMessageHandler(new Dictionary<string, string>(), statusCode: HttpStatusCode.InternalServerError);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.maxipali.co.cr") };
        var scraper = new VtexScraper("MaxiPalí Alajuela", "https://www.maxipali.co.cr", http, NullLogger<VtexScraper>.Instance);

        var results = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.Empty(results);
    }
}

internal class FakeHttpMessageHandler(Dictionary<string, string> responses, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";

        var matchedKey = responses.Keys.FirstOrDefault(k => path.Contains(k));
        var content = matchedKey is not null ? responses[matchedKey] : "[]";

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}

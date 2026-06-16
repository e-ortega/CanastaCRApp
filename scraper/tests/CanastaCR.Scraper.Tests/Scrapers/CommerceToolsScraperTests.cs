using CanastaCR.Scraper.Scrapers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CanastaCR.Scraper.Tests.Scrapers;

public class CommerceToolsScraperTests
{
    // Mirrors the real /api/ct/getProduct response shape confirmed live on 2026-06-16:
    // { data: { products: { total, results: [{ masterData: { current: {
    //     slug, name, allVariants: [{ sku, attributesRaw: [{name, value, ...}], availability }]
    // } } }] } } }
    // attributesRaw entries are CommerceTools "RawProductAttribute"s. "text"-typed attributes
    // (unit_price) double-encode their value as a JSON string; "set"-typed ones (localized_images)
    // store it as a structured value directly.
    private static string BuildResponse(int total, object[] results) => JsonSerializer.Serialize(new
    {
        data = new { products = new { offset = 0, count = results.Length, total, results } }
    });

    private static object MakeProduct(string name, string slug, string sku, string? brand,
        (string country, string club, string value)[] unitPriceEntries,
        bool crOnStock = true)
    {
        var unitPriceJson = JsonSerializer.Serialize(unitPriceEntries
            .Select(e => new { country = e.country, club = e.club, value = e.value }));

        var attributesRaw = new List<object>
        {
            new { name = "unit_price", value = unitPriceJson, attributeDefinition = new { type = new { name = "text" } } }
        };

        if (brand is not null)
            attributesRaw.Add(new { name = "brand", value = brand, attributeDefinition = new { type = new { name = "text" } } });

        return new
        {
            masterData = new
            {
                current = new
                {
                    slug,
                    name,
                    allVariants = new[]
                    {
                        new
                        {
                            sku,
                            attributesRaw,
                            availability = new
                            {
                                channels = new
                                {
                                    results = new[]
                                    {
                                        new
                                        {
                                            channel = new { key = "64", address = new { country = "CR" } },
                                            availability = new { isOnStock = crOnStock }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    [Fact]
    public async Task ScrapeAsync_ParsesProducts_WithCrPrice()
    {
        var product = MakeProduct(
            name: "Aceite Girasol 5L",
            slug: "aceite-girasol-5l-167401",
            sku: "167401",
            brand: "Member's Selection",
            unitPriceEntries: [("PA", "62", "13.99"), ("CR", "64", "6495.0")]);

        var page1 = BuildResponse(total: 1, results: [product]);

        var handler = new CallCountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(page1, Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(handler);
        var scraper = new CommerceToolsScraper(http, NullLogger<CommerceToolsScraper>.Instance);

        var results = await scraper.ScrapeAsync(ct: CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Aceite Girasol 5L", results[0].Name);
        Assert.Equal("Member's Selection", results[0].Brand);
        Assert.Equal(6495.0m, results[0].Price);
        Assert.Equal("CRC", results[0].Currency);
        Assert.Null(results[0].Barcode);
        Assert.True(results[0].IsAvailable);
    }

    [Fact]
    public async Task ScrapeAsync_SkipsProducts_NotSoldInCostaRica()
    {
        // Only has prices for Panama/Guatemala — should be filtered out, not written as a product
        var notInCr = MakeProduct(
            name: "Producto Solo Panama", slug: "solo-panama", sku: "999",
            brand: null, unitPriceEntries: [("PA", "62", "10.00"), ("GT", "63", "9.50")]);

        var inCr = MakeProduct(
            name: "Producto Costa Rica", slug: "en-cr", sku: "111",
            brand: null, unitPriceEntries: [("CR", "64", "1000.0")]);

        var page = BuildResponse(total: 2, results: [notInCr, inCr]);

        var handler = new CallCountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(page, Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(handler);
        var scraper = new CommerceToolsScraper(http, NullLogger<CommerceToolsScraper>.Instance);

        var results = await scraper.ScrapeAsync(ct: CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Producto Costa Rica", results[0].Name);
    }

    [Fact]
    public async Task ScrapeAsync_StopsAtMaxProducts()
    {
        var products = Enumerable.Range(0, 5)
            .Select(i => MakeProduct($"Producto {i}", $"slug-{i}", $"{i}", null, [("CR", "64", "100.0")]))
            .ToArray();

        var page = BuildResponse(total: 5, results: products);

        var handler = new CallCountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(page, Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(handler);
        var scraper = new CommerceToolsScraper(http, NullLogger<CommerceToolsScraper>.Instance);

        var results = await scraper.ScrapeAsync(maxProducts: 2, ct: CancellationToken.None);

        Assert.Equal(2, results.Count);
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

        var results = await scraper.ScrapeAsync(ct: CancellationToken.None);

        Assert.Empty(results);
    }
}

internal class CallCountingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(handler(request));
}

using AngleSharp;
using AngleSharp.Dom;
using CanastaCR.Scraper.Abstractions;
using System.Text.Json;
using System.Xml;

namespace CanastaCR.Scraper.Scrapers;

/// <summary>
/// Scrapes MegaSuper: fetches /sitemap.xml to enumerate product URLs,
/// then parses JSON-LD structured data from each SSR page.
/// EAN is the last segment of the product URL slug.
/// </summary>
public class JsonLdScraper(HttpClient http, ILogger<JsonLdScraper> logger) : IStoreScraper
{
    private const string BaseUrl = "https://www.megasuper.com";
    private static readonly IBrowsingContext AngleSharp = BrowsingContext.New(Configuration.Default);

    public string StoreName => "MegaSuper Tibás";
    public string Platform => "megasuper";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(int? maxProducts = null, CancellationToken ct = default)
    {
        logger.LogInformation("MegaSuper: starting JSON-LD scrape");

        var productUrls = await GetProductUrlsFromSitemap(ct);
        logger.LogInformation("MegaSuper: found {Count} product URLs in sitemap", productUrls.Count);

        var results = new List<ScrapedProduct>(productUrls.Count);

        foreach (var url in productUrls)
        {
            if (ct.IsCancellationRequested) break;
            if (maxProducts.HasValue && results.Count >= maxProducts.Value) break;

            try
            {
                var product = await ScrapeProductPage(url, ct);
                if (product is not null) results.Add(product);
                await Task.Delay(TimeSpan.FromMilliseconds(800), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MegaSuper: failed to scrape {Url}", url);
            }
        }

        logger.LogInformation("MegaSuper: scraped {Count} products", results.Count);
        return results;
    }

    private async Task<List<string>> GetProductUrlsFromSitemap(CancellationToken ct)
    {
        var urls = new List<string>();
        try
        {
            var xml = await http.GetStringAsync($"{BaseUrl}/sitemap.xml", ct);
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("sm", "http://www.sitemaps.org/schemas/sitemap/0.9");

            var nodes = doc.SelectNodes("//sm:url/sm:loc", ns);
            if (nodes is not null)
                foreach (XmlNode node in nodes)
                    if (node.InnerText.Contains("/p/"))
                        urls.Add(node.InnerText.Trim());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MegaSuper: failed to parse sitemap");
        }
        return urls;
    }

    private async Task<ScrapedProduct?> ScrapeProductPage(string url, CancellationToken ct)
    {
        var html = await http.GetStringAsync(url, ct);
        var document = await AngleSharp.OpenAsync(req => req.Content(html), ct);

        var ldScript = document.QuerySelector("script[type='application/ld+json']");
        if (ldScript is null) return null;

        using var json = JsonDocument.Parse(ldScript.TextContent);
        var root = json.RootElement;

        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var ean = root.TryGetProperty("sku", out var s) ? s.GetString() : ExtractEanFromUrl(url);
        var brand = root.TryGetProperty("brand", out var br) && br.TryGetProperty("name", out var bn)
            ? bn.GetString() : null;
        var imageUrl = root.TryGetProperty("image", out var img) ? img.GetString() : null;

        decimal price = 0;
        bool available = true;

        if (root.TryGetProperty("offers", out var offers))
        {
            if (offers.TryGetProperty("priceSpecification", out var priceSpec))
            {
                if (priceSpec.TryGetProperty("price", out var p))
                    price = p.GetDecimal();
            }
            else if (offers.TryGetProperty("price", out var directPrice))
            {
                price = directPrice.ValueKind == JsonValueKind.String
                    ? decimal.TryParse(directPrice.GetString(), out var parsed) ? parsed : 0
                    : directPrice.GetDecimal();
            }

            if (offers.TryGetProperty("availability", out var av))
                available = !av.GetString()?.Contains("OutOfStock", StringComparison.OrdinalIgnoreCase) ?? true;
        }

        return new ScrapedProduct(
            Name: name,
            Brand: brand,
            Barcode: ean,
            Price: price,
            Currency: "CRC",
            ImageUrl: imageUrl,
            Category: null,
            IsAvailable: available,
            SourceUrl: url
        );
    }

    private static string? ExtractEanFromUrl(string url)
    {
        // URL pattern: /p/{slug}-{ean}  — EAN is the last hyphen-separated segment
        var path = new Uri(url).AbsolutePath;
        var lastSegment = path.Split('/').LastOrDefault();
        if (string.IsNullOrEmpty(lastSegment)) return null;

        var parts = lastSegment.Split('-');
        var last = parts.LastOrDefault();
        return last is { Length: >= 8 } && last.All(char.IsDigit) ? last : null;
    }
}

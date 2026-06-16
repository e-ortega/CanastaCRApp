using CanastaCR.Scraper.Abstractions;
using System.Net.Http.Json;
using System.Text.Json;

namespace CanastaCR.Scraper.Scrapers;

/// <summary>
/// Scrapes VTEX-based stores: MaxiPalí and Más x Menos.
/// Parameterized by base URL so one class handles both.
/// </summary>
public class VtexScraper(string storeName, string baseUrl, HttpClient http, ILogger<VtexScraper> logger) : IStoreScraper
{
    private const int PageSize = 49;

    public string StoreName => storeName;

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("{Store}: starting VTEX scrape", storeName);
        var results = new List<ScrapedProduct>();

        var categories = await GetCategoryIdsAsync(ct);
        logger.LogInformation("{Store}: found {Count} categories", storeName, categories.Count);

        foreach (var categoryId in categories)
        {
            if (ct.IsCancellationRequested) break;
            await ScrapeCategory(categoryId, results, ct);
            await Task.Delay(TimeSpan.FromSeconds(1.5), ct);
        }

        logger.LogInformation("{Store}: scraped {Count} products total", storeName, results.Count);
        return results;
    }

    private async Task<List<int>> GetCategoryIdsAsync(CancellationToken ct)
    {
        var ids = new List<int>();
        var url = $"{baseUrl}/api/catalog_system/pub/category/tree/3";

        try
        {
            var tree = await http.GetFromJsonAsync<JsonElement[]>(url, ct);
            if (tree is null) return ids;

            foreach (var top in tree)
                CollectLeafIds(top, ids);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Store}: failed to get category tree", storeName);
        }

        return ids;
    }

    private static void CollectLeafIds(JsonElement node, List<int> ids)
    {
        if (!node.TryGetProperty("children", out var children) || children.GetArrayLength() == 0)
        {
            if (node.TryGetProperty("id", out var id))
                ids.Add(id.GetInt32());
            return;
        }

        foreach (var child in children.EnumerateArray())
            CollectLeafIds(child, ids);
    }

    private async Task ScrapeCategory(int categoryId, List<ScrapedProduct> results, CancellationToken ct)
    {
        var from = 0;
        while (!ct.IsCancellationRequested)
        {
            var url = $"{baseUrl}/api/catalog_system/pub/products/search?fq=C:{categoryId}&_from={from}&_to={from + PageSize}";

            try
            {
                var page = await http.GetFromJsonAsync<JsonElement[]>(url, ct);
                if (page is null || page.Length == 0) break;

                foreach (var item in page)
                    results.Add(ParseProduct(item));

                if (page.Length < PageSize + 1) break;
                from += PageSize + 1;

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Store}: failed page at category {CategoryId} from={From}", storeName, categoryId, from);
                break;
            }
        }
    }

    private ScrapedProduct ParseProduct(JsonElement item)
    {
        var name = item.TryGetProperty("productName", out var n) ? n.GetString() ?? "" : "";
        var brand = item.TryGetProperty("brand", out var b) ? b.GetString() : null;
        var ean = item.TryGetProperty("ean", out var e) ? e.GetString() : null;
        var category = item.TryGetProperty("categoryId", out var c) ? c.GetString() : null;

        decimal price = 0;
        bool available = false;
        string? imageUrl = null;
        string sourceUrl = "";

        if (item.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
        {
            var firstItem = items[0];

            if (firstItem.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                imageUrl = images[0].TryGetProperty("imageUrl", out var img) ? img.GetString() : null;

            if (firstItem.TryGetProperty("sellers", out var sellers) && sellers.GetArrayLength() > 0)
            {
                var seller = sellers[0];
                if (seller.TryGetProperty("commertialOffer", out var offer))
                {
                    if (offer.TryGetProperty("Price", out var p)) price = p.GetDecimal();
                    if (offer.TryGetProperty("IsAvailable", out var av)) available = av.GetBoolean();
                }
            }
        }

        if (item.TryGetProperty("link", out var link))
            sourceUrl = link.GetString() ?? "";

        return new ScrapedProduct(
            Name: name,
            Brand: brand,
            Barcode: string.IsNullOrEmpty(ean) ? null : ean,
            Price: price,
            Currency: "CRC",
            ImageUrl: imageUrl,
            Category: category,
            IsAvailable: available,
            SourceUrl: sourceUrl
        );
    }
}

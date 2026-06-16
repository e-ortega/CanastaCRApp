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
    public string Platform => "vtex";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(int? maxProducts = null, CancellationToken ct = default)
    {
        logger.LogInformation("{Store}: starting VTEX scrape", storeName);
        var results = new List<ScrapedProduct>();

        var categories = await GetCategoryIdsAsync(ct);
        logger.LogInformation("{Store}: found {Count} categories", storeName, categories.Count);

        // A capped run (smoke test / manual Postman check) makes far fewer total requests than
        // a full catalog crawl, so a shorter delay is still polite. Many categories are sparse
        // or out of stock, so the cap check must run right after each category too — otherwise
        // a single trailing delay after the category that already satisfied the cap, repeated
        // across however many empty categories came before it, can blow well past a smoke
        // test's time budget despite the scraper itself working correctly.
        var interCategoryDelay = maxProducts.HasValue ? TimeSpan.FromMilliseconds(300) : TimeSpan.FromSeconds(1.5);

        foreach (var categoryId in categories)
        {
            if (ct.IsCancellationRequested) break;
            if (maxProducts.HasValue && results.Count >= maxProducts.Value) break;

            await ScrapeCategory(categoryId, results, maxProducts, ct);

            if (maxProducts.HasValue && results.Count >= maxProducts.Value) break;
            await Task.Delay(interCategoryDelay, ct);
        }

        logger.LogInformation("{Store}: scraped {Count} products total", storeName, results.Count);
        return results;
    }

    /// <summary>
    /// Returns only the TOP-LEVEL category IDs, not leaf nodes. VTEX's products/search
    /// "fq=C:{id}" filter matches the category AND all of its descendants, so top-level IDs
    /// already cover the full catalog. Originally this recursed to leaf nodes instead (~860
    /// of them for MaxiPalí, vs. ~26 top-level) — confirmed live 2026-06-16 that the first 10
    /// leaf categories in tree order all returned zero products, while every sampled top-level
    /// category returned products on the first request. Leaf-level iteration was both far
    /// slower (860 HTTP round-trips + delays instead of ~26) and not even necessary.
    /// </summary>
    private async Task<List<int>> GetCategoryIdsAsync(CancellationToken ct)
    {
        var ids = new List<int>();
        var url = $"{baseUrl}/api/catalog_system/pub/category/tree/3";

        try
        {
            var tree = await http.GetFromJsonAsync<JsonElement[]>(url, ct);
            if (tree is null) return ids;

            foreach (var top in tree)
                if (top.TryGetProperty("id", out var id))
                    ids.Add(id.GetInt32());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Store}: failed to get category tree", storeName);
        }

        return ids;
    }

    private async Task ScrapeCategory(int categoryId, List<ScrapedProduct> results, int? maxProducts, CancellationToken ct)
    {
        var from = 0;
        while (!ct.IsCancellationRequested)
        {
            if (maxProducts.HasValue && results.Count >= maxProducts.Value) break;

            var url = $"{baseUrl}/api/catalog_system/pub/products/search?fq=C:{categoryId}&_from={from}&_to={from + PageSize}";

            try
            {
                var page = await http.GetFromJsonAsync<JsonElement[]>(url, ct);
                if (page is null || page.Length == 0) break;

                foreach (var item in page)
                {
                    results.Add(ParseProduct(item));
                    if (maxProducts.HasValue && results.Count >= maxProducts.Value) break;
                }

                if (maxProducts.HasValue && results.Count >= maxProducts.Value) break;
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
        var category = item.TryGetProperty("categoryId", out var c) ? c.GetString() : null;

        string? ean = null;
        decimal price = 0;
        bool available = false;
        string? imageUrl = null;
        string sourceUrl = "";

        if (item.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
        {
            var firstItem = items[0];

            // EAN lives on the SKU/variant level, not on the top-level product object
            if (firstItem.TryGetProperty("ean", out var e))
                ean = e.GetString();

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

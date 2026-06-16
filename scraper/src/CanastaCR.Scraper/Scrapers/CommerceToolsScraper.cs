using CanastaCR.Scraper.Abstractions;
using System.Net.Http.Json;
using System.Text.Json;

namespace CanastaCR.Scraper.Scrapers;

/// <summary>
/// Scrapes PriceSmart via the CommerceTools-backed /api/ct/getProduct endpoint.
/// Price is stored as centAmount (÷100 = CRC price).
/// </summary>
public class CommerceToolsScraper(HttpClient http, ILogger<CommerceToolsScraper> logger) : IStoreScraper
{
    private const string ApiUrl = "https://www.pricesmart.com/api/ct/getProduct";
    private const string Country = "CR";
    private const int PageSize = 50;

    public string StoreName => "PriceSmart San José";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("PriceSmart: starting CommerceTools scrape");
        var results = new List<ScrapedProduct>();
        var offset = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var body = new { country = Country, offset, limit = PageSize };
                var response = await http.PostAsJsonAsync(ApiUrl, body, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

                if (!json.TryGetProperty("results", out var resultsEl) || resultsEl.GetArrayLength() == 0)
                    break;

                foreach (var item in resultsEl.EnumerateArray())
                {
                    var product = ParseProduct(item);
                    if (product is not null) results.Add(product);
                }

                var count = resultsEl.GetArrayLength();
                logger.LogDebug("PriceSmart: page offset={Offset} returned {Count} products", offset, count);

                if (count < PageSize) break;
                offset += PageSize;
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PriceSmart: failed at offset {Offset}", offset);
                break;
            }
        }

        logger.LogInformation("PriceSmart: scraped {Count} products", results.Count);
        return results;
    }

    private ScrapedProduct? ParseProduct(JsonElement item)
    {
        try
        {
            var name = GetLocalizedString(item, "name") ?? "";
            if (string.IsNullOrEmpty(name)) return null;

            var slug = GetLocalizedString(item, "slug");

            decimal price = 0;
            bool available = true;

            // Price lives in masterVariant.prices[0].value.centAmount
            if (item.TryGetProperty("masterVariant", out var variant))
            {
                if (variant.TryGetProperty("prices", out var prices) && prices.GetArrayLength() > 0)
                {
                    var firstPrice = prices[0];
                    if (firstPrice.TryGetProperty("value", out var value) &&
                        value.TryGetProperty("centAmount", out var centAmount))
                    {
                        price = centAmount.GetInt64() / 100m;
                    }
                }

                if (variant.TryGetProperty("availability", out var avail))
                    available = avail.TryGetProperty("isOnStock", out var stock) && stock.GetBoolean();
            }

            // Try to extract barcode from attributes
            string? barcode = null;
            if (item.TryGetProperty("masterVariant", out var v2) &&
                v2.TryGetProperty("attributes", out var attrs))
            {
                foreach (var attr in attrs.EnumerateArray())
                {
                    if (attr.TryGetProperty("name", out var attrName) &&
                        attrName.GetString() is "ean" or "gtin" or "barcode" or "upc")
                    {
                        if (attr.TryGetProperty("value", out var attrVal))
                            barcode = attrVal.GetString();
                        break;
                    }
                }
            }

            var sourceUrl = slug is not null ? $"https://www.pricesmart.com/en-cr/product/{slug}" : "";

            return new ScrapedProduct(
                Name: name,
                Brand: null,
                Barcode: barcode,
                Price: price,
                Currency: "CRC",
                ImageUrl: GetImageUrl(item),
                Category: null,
                IsAvailable: available,
                SourceUrl: sourceUrl
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PriceSmart: failed to parse a product");
            return null;
        }
    }

    private static string? GetLocalizedString(JsonElement element, string propName)
    {
        if (!element.TryGetProperty(propName, out var prop)) return null;

        // Try es-CR, es, en
        foreach (var lang in new[] { "es-CR", "es", "en" })
            if (prop.TryGetProperty(lang, out var val))
                return val.GetString();

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    private static string? GetImageUrl(JsonElement item)
    {
        if (!item.TryGetProperty("masterVariant", out var variant)) return null;
        if (!variant.TryGetProperty("images", out var images) || images.GetArrayLength() == 0) return null;

        return images[0].TryGetProperty("url", out var url) ? url.GetString() : null;
    }
}

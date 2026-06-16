using CanastaCR.Core.Enums;
using CanastaCR.Scraper.Abstractions;
using System.Net.Http.Json;
using System.Text.Json;

namespace CanastaCR.Scraper.Scrapers;

/// <summary>
/// Scrapes PriceSmart via the CommerceTools-backed /api/ct/getProduct endpoint.
///
/// This endpoint returns PriceSmart's ENTIRE multi-country catalog (~12,021 products across
/// Panama, Guatemala, Honduras, Nicaragua, Jamaica, Trinidad, Dominican Republic, Colombia,
/// Aruba, Barbados, El Salvador, Costa Rica, US Virgin Islands) regardless of the requested
/// "country"/"club"/"channelKey" body parameters — none of them filter server-side (confirmed
/// live 2026-06-16). Most products are NOT sold in Costa Rica. There is no known way to ask for
/// CR-only results, so every product must be fetched and filtered client-side by checking
/// whether its "unit_price" attribute has a "CR" entry. See docs/ARCHITECTURE.md section 7 for
/// the schema discovery and the bandwidth cost this implies (~3GB for a full nightly scan).
/// </summary>
public class CommerceToolsScraper(HttpClient http, ILogger<CommerceToolsScraper> logger) : IStoreScraper
{
    private const string ApiUrl = "https://www.pricesmart.com/api/ct/getProduct";
    private const string Country = "CR";
    private const string CrAggregateClub = "64"; // country-wide club code, present alongside per-store clubs
    private const int PageSize = 50;

    public StoreChain Chain => StoreChain.PriceSmart;
    public string StoreName => Chain.GetDisplayName();
    public string Platform => "pricesmart";

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(int? maxProducts = null, CancellationToken ct = default)
    {
        logger.LogInformation("PriceSmart: starting full-catalog scan with client-side CR filter");
        var results = new List<ScrapedProduct>();
        var offset = 0;
        var scanned = 0;
        int? total = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var body = new { country = Country, offset, limit = PageSize };
                var response = await http.PostAsJsonAsync(ApiUrl, body, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

                if (!json.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("products", out var productsEl) ||
                    !productsEl.TryGetProperty("results", out var resultsEl) ||
                    resultsEl.GetArrayLength() == 0)
                {
                    break;
                }

                if (total is null && productsEl.TryGetProperty("total", out var totalEl))
                    total = totalEl.GetInt32();

                var pageCount = resultsEl.GetArrayLength();
                scanned += pageCount;

                foreach (var item in resultsEl.EnumerateArray())
                {
                    var product = ParseProduct(item);
                    if (product is not null)
                        results.Add(product);

                    if (maxProducts.HasValue && results.Count >= maxProducts.Value)
                        break;
                }

                if (maxProducts.HasValue && results.Count >= maxProducts.Value)
                    break;

                offset += PageSize;
                if (total.HasValue && offset >= total.Value)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PriceSmart: failed at offset {Offset}", offset);
                break;
            }
        }

        logger.LogInformation(
            "PriceSmart: {CrCount} CR-available products found after scanning {Scanned}/{Total} catalog entries",
            results.Count, scanned, total);
        return results;
    }

    private ScrapedProduct? ParseProduct(JsonElement item)
    {
        try
        {
            if (!item.TryGetProperty("masterData", out var masterData) ||
                !masterData.TryGetProperty("current", out var current))
                return null;

            var name = current.TryGetProperty("name", out var n) ? n.GetString()?.Trim() ?? "" : "";
            if (string.IsNullOrEmpty(name)) return null;

            var slug = current.TryGetProperty("slug", out var s) ? s.GetString()?.Trim() : null;

            if (!current.TryGetProperty("allVariants", out var variants) || variants.GetArrayLength() == 0)
                return null;

            var variant = variants[0];

            if (!variant.TryGetProperty("attributesRaw", out var attrs))
                return null;

            // Most of PriceSmart's catalog isn't sold in Costa Rica — skip anything without a CR price.
            var crPrice = FindCrPrice(attrs);
            if (crPrice is null) return null;

            var brand = GetAttributeRawValue(attrs, "brand") is { ValueKind: JsonValueKind.String } brandEl
                ? brandEl.GetString()
                : null;

            return new ScrapedProduct(
                Name: name,
                Brand: brand,
                Barcode: null, // PriceSmart exposes only an internal SKU/item number, no EAN/UPC — confirmed 2026-06-16
                Price: crPrice.Value,
                Currency: "CRC",
                ImageUrl: GetCrImageUrl(attrs),
                Category: null,
                IsAvailable: IsAvailableInCr(variant),
                SourceUrl: slug is not null ? $"https://www.pricesmart.com/en-cr/product/{slug}" : ""
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PriceSmart: failed to parse a product");
            return null;
        }
    }

    /// <summary>
    /// "unit_price" is a "text"-typed attribute whose value is itself a JSON-encoded string:
    /// [{ "country", "club", "value" }, ...] — one entry per club/store, plus a country-wide
    /// aggregate entry where club equals the country's own code (e.g. "64" for Costa Rica).
    /// </summary>
    private static decimal? FindCrPrice(JsonElement attributesRaw)
    {
        var valueEl = GetAttributeRawValue(attributesRaw, "unit_price");
        if (valueEl is not { ValueKind: JsonValueKind.String } stringEl) return null;

        using var doc = JsonDocument.Parse(stringEl.GetString()!);
        decimal? fallback = null;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("country", out var country) || country.GetString() != Country) continue;
            if (!entry.TryGetProperty("value", out var v) || !decimal.TryParse(v.GetString(), out var price)) continue;

            var club = entry.TryGetProperty("club", out var c) ? c.GetString() : null;
            if (club == CrAggregateClub) return price;
            fallback ??= price;
        }

        return fallback;
    }

    /// <summary>
    /// Per-club availability lives on variant.availability.channels.results[], keyed by
    /// channel.address.country — not inside attributesRaw like price.
    /// </summary>
    private static bool IsAvailableInCr(JsonElement variant)
    {
        if (!variant.TryGetProperty("availability", out var availability)) return true;
        if (!availability.TryGetProperty("channels", out var channels)) return true;
        if (!channels.TryGetProperty("results", out var channelResults)) return true;

        foreach (var entry in channelResults.EnumerateArray())
        {
            if (!entry.TryGetProperty("channel", out var channel)) continue;
            if (!channel.TryGetProperty("address", out var address)) continue;
            if (!address.TryGetProperty("country", out var country) || country.GetString() != Country) continue;
            if (!entry.TryGetProperty("availability", out var avail)) continue;

            if (avail.TryGetProperty("isOnStock", out var stock) && stock.GetBoolean())
                return true; // any CR club in stock counts as available
        }

        return false;
    }

    /// <summary>
    /// "localized_images" is a "set"-typed attribute whose value is already a structured array
    /// (unlike "text"-typed attributes like unit_price, which double-encode as a JSON string).
    /// </summary>
    private static string? GetCrImageUrl(JsonElement attributesRaw)
    {
        var valueEl = GetAttributeRawValue(attributesRaw, "localized_images");
        if (valueEl is not { ValueKind: JsonValueKind.Array } arr || arr.GetArrayLength() == 0) return null;

        var entry = arr[0];
        foreach (var locale in new[] { "es-CR", "en-CR" })
            if (entry.TryGetProperty(locale, out var url))
                return url.GetString();

        return null;
    }

    private static JsonElement? GetAttributeRawValue(JsonElement attributesRaw, string name)
    {
        foreach (var attr in attributesRaw.EnumerateArray())
        {
            if (attr.TryGetProperty("name", out var n) && n.GetString() == name &&
                attr.TryGetProperty("value", out var v))
                return v;
        }
        return null;
    }
}

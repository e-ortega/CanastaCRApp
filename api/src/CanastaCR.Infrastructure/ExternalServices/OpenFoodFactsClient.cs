using System.Text.Json;
using CanastaCR.Core.DTOs;
using CanastaCR.Core.Interfaces;

namespace CanastaCR.Infrastructure.ExternalServices;

public class OpenFoodFactsClient(HttpClient httpClient) : IOpenFoodFactsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<CreateProductDto?> LookupByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"https://world.openfoodfacts.org/api/v2/product/{barcode}?fields=product_name,brands,categories_tags,image_url",
            ct);

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("status", out var status) || status.GetInt32() != 1)
            return null;

        var product = root.GetProperty("product");

        var name = GetString(product, "product_name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var brand = GetString(product, "brands");
        var imageUrl = GetString(product, "image_url");

        string? category = null;
        if (product.TryGetProperty("categories_tags", out var cats) && cats.ValueKind == JsonValueKind.Array)
        {
            var first = cats.EnumerateArray()
                .Select(c => c.GetString())
                .FirstOrDefault(c => c != null && c.StartsWith("en:"));
            category = first?.Replace("en:", "");
        }

        return new CreateProductDto(barcode, name, brand, category, imageUrl, null);
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }
}

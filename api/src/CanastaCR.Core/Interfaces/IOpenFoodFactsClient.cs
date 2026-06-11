using CanastaCR.Core.DTOs;

namespace CanastaCR.Core.Interfaces;

public interface IOpenFoodFactsClient
{
    Task<CreateProductDto?> LookupByBarcodeAsync(string barcode, CancellationToken ct = default);
}

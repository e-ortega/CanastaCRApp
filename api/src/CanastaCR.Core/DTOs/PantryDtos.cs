using CanastaCR.Core.Enums;

namespace CanastaCR.Core.DTOs;

public record PantryItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string? ProductImageUrl,
    decimal Quantity,
    QuantityUnit Unit,
    decimal MinThreshold,
    bool IsRunningLow,
    DateTimeOffset? LastPurchasedAt
);

public record UpsertPantryItemDto(
    Guid ProductId,
    decimal Quantity,
    QuantityUnit Unit = QuantityUnit.Unit,
    decimal MinThreshold = 1
);

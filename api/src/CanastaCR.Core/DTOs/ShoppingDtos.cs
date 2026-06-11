using CanastaCR.Core.Enums;

namespace CanastaCR.Core.DTOs;

public record ShoppingListDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ShoppingListItemDto> Items
);

public record ShoppingListItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string? ProductImageUrl,
    decimal Quantity,
    QuantityUnit Unit,
    bool IsPurchased
);

public record CreateShoppingListDto(string Name);

public record AddShoppingListItemDto(
    Guid ProductId,
    decimal Quantity = 1,
    QuantityUnit Unit = QuantityUnit.Unit
);

public record ShoppingOptimizationResultDto(
    Guid ShoppingListId,
    decimal TotalEstimatedCost,
    decimal TotalIfSingleStore,
    decimal TotalSavings,
    decimal SavingsPercent,
    IReadOnlyList<StoreShoppingGroupDto> StoreGroups
);

public record StoreShoppingGroupDto(
    Guid StoreId,
    string StoreName,
    StoreChain Chain,
    decimal GroupTotal,
    IReadOnlyList<OptimizedItemDto> Items
);

public record OptimizedItemDto(
    Guid ProductId,
    string ProductName,
    decimal Price,
    decimal Quantity,
    QuantityUnit Unit,
    decimal LineTotal,
    decimal SavingsVsHighest
);

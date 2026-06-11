using CanastaCR.Core.Enums;

namespace CanastaCR.Core.DTOs;

public record PriceReportDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid StoreId,
    string StoreName,
    StoreChain StoreChain,
    decimal Price,
    string Currency,
    PriceSource Source,
    DateTimeOffset ReportedAt,
    DateTimeOffset ExpiresAt,
    bool IsExpired
);

public record CreatePriceReportDto(
    Guid ProductId,
    Guid StoreId,
    decimal Price,
    string Currency = "CRC"
);

public record StorePriceDto(
    Guid StoreId,
    string StoreName,
    StoreChain Chain,
    decimal Price,
    string Currency,
    DateTimeOffset ReportedAt,
    bool IsExpired
);

public record SavingsSummaryDto(
    int TotalProducts,
    int ProductsWithPriceGap,
    decimal TotalPotentialSavings,
    decimal AvgSavingsPercent,
    IReadOnlyList<TopDealDto> TopDeals
);

public record TopDealDto(
    Guid ProductId,
    string ProductName,
    string CheapestStore,
    decimal CheapestPrice,
    string MostExpensiveStore,
    decimal MostExpensivePrice,
    decimal SavingsAmount,
    decimal SavingsPercent
);

public record ProductPriceComparisonDto(
    Guid ProductId,
    string ProductName,
    string? ProductImageUrl,
    IReadOnlyList<StorePriceDto> Prices,
    decimal? LowestPrice,
    decimal? HighestPrice,
    decimal? SavingsAmount,
    decimal? SavingsPercent
);

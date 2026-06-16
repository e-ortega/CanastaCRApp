namespace CanastaCR.Core.DTOs;

public record ProductDto(
    Guid Id,
    string? Barcode,
    string Name,
    string? Brand,
    string? Category,
    string? ImageUrl,
    string? Description
);

public record CreateProductDto(
    string? Barcode,
    string Name,
    string? Brand,
    string? Category,
    string? ImageUrl,
    string? Description
);

public record UpdateProductDto(
    string Name
);

public record ProductSearchResultDto(
    Guid Id,
    string? Barcode,
    string Name,
    string? Brand,
    string? Category,
    string? ImageUrl,
    decimal? LowestPrice,
    string? LowestPriceStore
);

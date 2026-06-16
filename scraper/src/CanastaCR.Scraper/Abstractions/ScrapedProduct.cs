namespace CanastaCR.Scraper.Abstractions;

public record ScrapedProduct(
    string Name,
    string? Brand,
    string? Barcode,
    decimal Price,
    string Currency,
    string? ImageUrl,
    string? Category,
    bool IsAvailable,
    string SourceUrl
);

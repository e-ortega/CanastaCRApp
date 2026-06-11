using CanastaCR.Core.Enums;

namespace CanastaCR.Core.DTOs;

public record StoreDto(
    Guid Id,
    string Name,
    StoreChain Chain,
    string? Address,
    string City,
    double? Lat,
    double? Lng
);

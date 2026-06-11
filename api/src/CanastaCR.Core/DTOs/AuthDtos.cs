namespace CanastaCR.Core.DTOs;

public record RegisterDto(string Email, string DisplayName, string Password);

public record LoginDto(string Email, string Password);

public record AuthResultDto(string Token, string DisplayName, Guid UserId);

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CanastaCR.Core.DTOs;
using CanastaCR.Core.Entities;
using CanastaCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CanastaCR.Api.Services;

public class AuthService(AppDbContext db, IConfiguration config)
{
    public async Task<AuthResultDto?> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == dto.Email, ct))
            return null;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        db.UserPreferences.Add(new UserPreferences { UserId = user.Id });
        await db.SaveChangesAsync(ct);

        return new AuthResultDto(GenerateToken(user), user.DisplayName, user.Id);
    }

    public async Task<AuthResultDto?> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return null;

        return new AuthResultDto(GenerateToken(user), user.DisplayName, user.Id);
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

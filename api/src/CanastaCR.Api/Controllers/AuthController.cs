using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using CanastaCR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanastaCR.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, AppDbContext db) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(dto, ct);
        if (result is null) return Conflict(new { error = "Email already in use." });
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken ct)
    {
        var result = await authService.LoginAsync(dto, ct);
        if (result is null) return Unauthorized(new { error = "Invalid credentials." });
        return Ok(result);
    }

    [Authorize]
    [HttpGet("/api/users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await db.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.ReputationPoints, u.CreatedAt))
            .ToListAsync(ct);
        return Ok(users);
    }
}

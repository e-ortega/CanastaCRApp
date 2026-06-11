using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CanastaCR.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
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
}

using CanastaCR.Api.Services;
using CanastaCR.Core.DTOs;
using CanastaCR.Tests.Helpers;

namespace CanastaCR.Tests.Services;

public class AuthServiceTests
{
    private AuthService CreateService() =>
        new(DbContextFactory.Create(), ConfigurationFactory.CreateJwtConfig());

    [Fact]
    public async Task Register_CreatesUser_AndReturnsToken()
    {
        var service = CreateService();

        var result = await service.RegisterAsync(new RegisterDto("user@test.com", "Test User", "password123"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal("Test User", result.DisplayName);
    }

    [Fact]
    public async Task Register_HashesPassword_NotStoredAsPlainText()
    {
        var db = DbContextFactory.Create();
        var service = new AuthService(db, ConfigurationFactory.CreateJwtConfig());

        await service.RegisterAsync(new RegisterDto("user@test.com", "Test User", "mypassword"));

        var user = db.Users.First();
        Assert.NotEqual("mypassword", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("mypassword", user.PasswordHash));
    }

    [Fact]
    public async Task Register_ReturnsNull_WhenEmailAlreadyExists()
    {
        var db = DbContextFactory.Create();
        var service = new AuthService(db, ConfigurationFactory.CreateJwtConfig());

        await service.RegisterAsync(new RegisterDto("dup@test.com", "First", "pass123"));
        var result = await service.RegisterAsync(new RegisterDto("dup@test.com", "Second", "pass123"));

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_ReturnsToken_WithCorrectCredentials()
    {
        var db = DbContextFactory.Create();
        var service = new AuthService(db, ConfigurationFactory.CreateJwtConfig());

        await service.RegisterAsync(new RegisterDto("user@test.com", "Test User", "correct-pass"));
        var result = await service.LoginAsync(new LoginDto("user@test.com", "correct-pass"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task Login_ReturnsNull_WithWrongPassword()
    {
        var db = DbContextFactory.Create();
        var service = new AuthService(db, ConfigurationFactory.CreateJwtConfig());

        await service.RegisterAsync(new RegisterDto("user@test.com", "Test User", "correct-pass"));
        var result = await service.LoginAsync(new LoginDto("user@test.com", "wrong-pass"));

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_ReturnsNull_WhenUserDoesNotExist()
    {
        var service = CreateService();

        var result = await service.LoginAsync(new LoginDto("nobody@test.com", "pass"));

        Assert.Null(result);
    }
}

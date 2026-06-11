using Microsoft.Extensions.Configuration;

namespace CanastaCR.Tests.Helpers;

public static class ConfigurationFactory
{
    public static IConfiguration CreateJwtConfig()
    {
        var data = new Dictionary<string, string?>
        {
            ["Jwt:Key"]      = "test-super-secret-key-at-least-32-chars-long!",
            ["Jwt:Issuer"]   = "canastacr-test",
            ["Jwt:Audience"] = "canastacr-test",
        };
        return new ConfigurationBuilder()
            .Add(new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource { InitialData = data })
            .Build();
    }
}

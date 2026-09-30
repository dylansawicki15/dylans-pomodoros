using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PersonalLifeApi.Tests;

// The real app, booted in memory with fake settings and its own throwaway database.
// Every test gets its app from here, so a new required setting is added in one place.
public class TestApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-key";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"personal-life-api-test-{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Api:Key", ApiKey);
        builder.UseSetting("Google:ClientId", "fake");
        builder.UseSetting("Google:ClientSecret", "fake");
        builder.UseSetting("Google:RefreshToken", "fake");
        builder.UseSetting("ConnectionStrings:Feed", $"DataSource={_dbPath}");
    }
}

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PersonalLifeApi.Tests;

public class HealthTests
{
    [Fact]
    public async Task Health_answers_without_a_key()
    {
        // A fresh database file per test, so no test sees real data or another test's rows.
        var dbPath = Path.Combine(Path.GetTempPath(), $"personal-life-api-test-{Guid.NewGuid()}.db");

        // Boots the real Program.cs in memory. "Testing" is not Development, so user-secrets
        // stay unloaded and every required setting has to come from here.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Api:Key", "test-key");
            builder.UseSetting("Google:ClientId", "fake");
            builder.UseSetting("Google:ClientSecret", "fake");
            builder.UseSetting("Google:RefreshToken", "fake");
            builder.UseSetting("ConnectionStrings:Feed", $"DataSource={dbPath}");
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

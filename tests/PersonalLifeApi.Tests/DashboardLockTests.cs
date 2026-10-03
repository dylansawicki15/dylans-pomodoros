using System.Net;

namespace PersonalLifeApi.Tests;

// The test app runs as "Testing", not "Development", so it stands in for Fly: the dashboard must still answer 401.
public class DashboardLockTests
{
    // Runs once per address: the page, Blazor's script, and the live connection all stay locked.
    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/_blazor/negotiate")]
    public async Task Dashboard_is_locked_outside_Development(string path)
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

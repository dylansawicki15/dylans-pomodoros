using System.Net;

namespace PersonalLifeApi.Tests;

// The test app runs as "Testing", not "Development", so it stands in for Fly: the dashboard must still answer 401.
public class DashboardLockTests
{
    [Fact]
    public async Task Dashboard_is_locked_outside_Development()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

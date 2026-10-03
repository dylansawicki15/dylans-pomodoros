using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PersonalLifeApi.Tests;

// The dashboard's lock is the sign-in cookie, and the machine routes still need the key.
public class DashboardLockTests
{
    [Fact]
    public async Task Logged_out_visitor_is_sent_to_Google_instead_of_the_page()
    {
        using var factory = new TestApiFactory();
        // Don't follow redirects: the test wants to see where it points, not go there.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("accounts.google.com", response.Headers.Location?.Host);
    }

    [Fact]
    public async Task Stats_still_need_the_key()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/stats/pomodoros");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

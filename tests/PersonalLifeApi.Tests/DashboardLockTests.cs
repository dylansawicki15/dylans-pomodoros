using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalLifeApi.Tests;

// The dashboard's lock is the sign-in cookie plus the OnlyMe rule, and the machine routes still need the key.
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
    public async Task Only_the_allowed_email_passes_the_OnlyMe_rule()
    {
        using var factory = new TestApiFactory();
        // The same rule-checker [Authorize] uses, taken from the app's services.
        var rules = factory.Services.GetRequiredService<IAuthorizationService>();

        var me = SignedInAs(TestApiFactory.AllowedEmail);
        var stranger = SignedInAs("stranger@example.com");

        Assert.True((await rules.AuthorizeAsync(me, "OnlyMe")).Succeeded);
        Assert.False((await rules.AuthorizeAsync(stranger, "OnlyMe")).Succeeded);
    }

    [Fact]
    public async Task Stats_still_need_the_key()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/stats/pomodoros");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A pretend signed-in user carrying one claim, their email, like the one Google puts in your cookie.
    private static ClaimsPrincipal SignedInAs(string email) =>
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email)], "Test"));
}

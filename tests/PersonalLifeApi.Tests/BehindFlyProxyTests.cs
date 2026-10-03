using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PersonalLifeApi.Tests;

// On Fly the app only ever sees plain http. The proxy's X-Forwarded-Proto note must win, or Google refuses the return address.
public class BehindFlyProxyTests
{
    [Fact]
    public async Task Sign_in_return_address_uses_https_when_Fly_says_so()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://personal-life-api.fly.dev"),
        });
        // What Fly's proxy does: pass the request on as http, with a note saying it arrived as https.
        var request = new HttpRequestMessage(HttpMethod.Get, "/login");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        var toGoogle = HttpUtility.ParseQueryString(response.Headers.Location!.Query);
        Assert.Equal("https://personal-life-api.fly.dev/signin-google", toGoogle["redirect_uri"]);
    }
}

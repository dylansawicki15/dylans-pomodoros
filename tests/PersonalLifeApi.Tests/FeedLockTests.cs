using System.Net;

namespace PersonalLifeApi.Tests;

public class FeedLockTests
{
    [Fact]
    public async Task Feed_rejects_a_missing_key()
    {
        using var factory = new TestApiFactory();

        var client = factory.CreateClient();
        var response = await client.GetAsync("/feed");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Feed_rejects_a_wrong_key()
    {
        using var factory = new TestApiFactory();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "not-the-key");

        var response = await client.GetAsync("/feed");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Feed_accepts_the_right_key()
    {
        using var factory = new TestApiFactory();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", TestApiFactory.ApiKey);

        var response = await client.GetAsync("/feed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

using System.Net;

namespace PersonalLifeApi.Tests;

public class HealthTests
{
    [Fact]
    public async Task Health_answers_without_a_key()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

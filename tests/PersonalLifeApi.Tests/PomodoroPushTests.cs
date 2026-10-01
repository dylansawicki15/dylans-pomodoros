using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PersonalLifeApi.Tests;

public class PomodoroPushTests
{
    [Fact]
    public async Task Pushing_the_same_pomodoro_twice_stores_it_once()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", TestApiFactory.ApiKey);

        // Shaped like the pusher's body: YAPA's id, minutes, and an end time that states its offset.
        var pomodoros = new[]
        {
            new { id = 1, profileName = "test", duration = 25, endTime = "2026-09-30T10:00:00+00:00" }
        };

        var first = await client.PostAsJsonAsync("/pomodoros", pomodoros);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/pomodoros", pomodoros);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var feed = await client.GetFromJsonAsync<JsonElement>("/feed");

        Assert.Equal(1, feed.GetArrayLength());
    }
}

using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalLifeApi.Tests;

public class PomodoroStatsTests
{
    [Fact]
    public async Task Today_is_counted_in_amsterdam_days_not_utc_days()
    {
        // 10:00 UTC is 12:00 in Amsterdam on 2 October.
        var now = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);

        using var baseFactory = new TestApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedClock(now))));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FeedDbContext>();
            // 23:30 Amsterdam on 1 October: yesterday.
            db.FeedItems.Add(new FeedItem { Source = "yapa", SourceId = "yesterday", Starts = new DateTimeOffset(2026, 10, 1, 21, 30, 0, TimeSpan.Zero), Ends = new DateTimeOffset(2026, 10, 1, 21, 55, 0, TimeSpan.Zero) });
            // 00:05 Amsterdam on 2 October: today, though its UTC date is still 1 October.
            db.FeedItems.Add(new FeedItem { Source = "yapa", SourceId = "after-midnight", Starts = new DateTimeOffset(2026, 10, 1, 22, 5, 0, TimeSpan.Zero), Ends = new DateTimeOffset(2026, 10, 1, 22, 30, 0, TimeSpan.Zero) });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", TestApiFactory.ApiKey);

        var stats = await client.GetFromJsonAsync<PomodoroStatsResponse>("/stats/pomodoros");

        Assert.Equal(25, stats!.TodayMinutes);
    }

    // A clock stuck at one moment, so "today" is the same every time the test runs.
    private class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

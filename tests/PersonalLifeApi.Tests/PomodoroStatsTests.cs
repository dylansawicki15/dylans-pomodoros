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
        Assert.Equal(50, stats!.WeekMinutes);
        Assert.Equal(50, stats!.MonthMinutes);
        Assert.Equal(50, stats!.YearMinutes);
    }

    [Fact]
    public async Task Weeks_start_on_sunday_and_a_pomodoro_counts_on_the_day_it_started()
    {
        // Friday 2 October, 12:00 in Amsterdam: the same "now" as above.
        var now = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        // Amsterdam's offset in late September (summer time), so the seed reads as your clock.
        var amsterdam = TimeSpan.FromHours(2);

        using var baseFactory = new TestApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedClock(now))));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FeedDbContext>();
            // Saturday 26 Sep 23:50 to Sunday 27 Sep 00:15: crosses midnight into Sunday.
            db.FeedItems.Add(new FeedItem { Source = "yapa", SourceId = "saturday-night", Starts = new DateTimeOffset(2026, 9, 26, 23, 50, 0, amsterdam), Ends = new DateTimeOffset(2026, 9, 27, 0, 15, 0, amsterdam) });
            // Sunday 27 Sep 00:05 to 00:30: just after midnight on Sunday.
            db.FeedItems.Add(new FeedItem { Source = "yapa", SourceId = "sunday-early", Starts = new DateTimeOffset(2026, 9, 27, 0, 5, 0, amsterdam), Ends = new DateTimeOffset(2026, 9, 27, 0, 30, 0, amsterdam) });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", TestApiFactory.ApiKey);

        var stats = await client.GetFromJsonAsync<PomodoroStatsResponse>("/stats/pomodoros");

        Assert.Equal(0, stats!.TodayMinutes);
        Assert.Equal(25, stats!.WeekMinutes);
        Assert.Equal(0, stats!.MonthMinutes);
        Assert.Equal(50, stats!.YearMinutes);
    }

    // A clock stuck at one moment, so "today" is the same every time the test runs.
    private class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

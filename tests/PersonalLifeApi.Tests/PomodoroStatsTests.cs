using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalLifeApi.Tests;

public class PomodoroStatsTests
{
    // Friday 2 October, 12:00 in Amsterdam (10:00 UTC). Every test here asks for the stats at this moment.
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);

    // Amsterdam's offset in late September and early October (summer time), so the seeds read as your clock.
    private static readonly TimeSpan Amsterdam = TimeSpan.FromHours(2);

    [Fact]
    public async Task Today_is_counted_in_amsterdam_days_not_utc_days()
    {
        var stats = await GetStatsAsync(
            // 23:30 Amsterdam on 1 October: yesterday.
            new FeedItem { Source = "yapa", SourceId = "yesterday", Starts = new DateTimeOffset(2026, 10, 1, 21, 30, 0, TimeSpan.Zero), Ends = new DateTimeOffset(2026, 10, 1, 21, 55, 0, TimeSpan.Zero) },
            // 00:05 Amsterdam on 2 October: today, though its UTC date is still 1 October.
            new FeedItem { Source = "yapa", SourceId = "after-midnight", Starts = new DateTimeOffset(2026, 10, 1, 22, 5, 0, TimeSpan.Zero), Ends = new DateTimeOffset(2026, 10, 1, 22, 30, 0, TimeSpan.Zero) });

        Assert.Equal(25, stats.TodayMinutes);
        Assert.Equal(50, stats.WeekMinutes);
        Assert.Equal(50, stats.MonthMinutes);
        Assert.Equal(50, stats.YearMinutes);
    }

    [Fact]
    public async Task Weeks_start_on_sunday_and_a_pomodoro_counts_on_the_day_it_started()
    {
        var stats = await GetStatsAsync(
            // Saturday 26 Sep 23:50 to Sunday 27 Sep 00:15: crosses midnight into Sunday.
            new FeedItem { Source = "yapa", SourceId = "saturday-night", Starts = new DateTimeOffset(2026, 9, 26, 23, 50, 0, Amsterdam), Ends = new DateTimeOffset(2026, 9, 27, 0, 15, 0, Amsterdam) },
            // Sunday 27 Sep 00:05 to 00:30: just after midnight on Sunday.
            new FeedItem { Source = "yapa", SourceId = "sunday-early", Starts = new DateTimeOffset(2026, 9, 27, 0, 5, 0, Amsterdam), Ends = new DateTimeOffset(2026, 9, 27, 0, 30, 0, Amsterdam) });

        Assert.Equal(0, stats.TodayMinutes);
        Assert.Equal(25, stats.WeekMinutes);
        Assert.Equal(0, stats.MonthMinutes);
        Assert.Equal(50, stats.YearMinutes);
    }

    [Fact]
    public async Task Projection_keeps_up_the_pace_of_the_last_7_days()
    {
        // Three pomodoros, each 35 minutes long (20:00 to 20:35, or 09:00 to 09:35).
        var stats = await GetStatsAsync(
            // Friday 25 Sep: 7 days before today.
            new FeedItem { Source = "yapa", SourceId = "friday-before", Starts = new DateTimeOffset(2026, 9, 25, 20, 0, 0, Amsterdam), Ends = new DateTimeOffset(2026, 9, 25, 20, 35, 0, Amsterdam) },
            // Saturday 26 Sep: 6 days before today.
            new FeedItem { Source = "yapa", SourceId = "saturday", Starts = new DateTimeOffset(2026, 9, 26, 20, 0, 0, Amsterdam), Ends = new DateTimeOffset(2026, 9, 26, 20, 35, 0, Amsterdam) },
            // Friday 2 Oct: today, before the 12:00 "now".
            new FeedItem { Source = "yapa", SourceId = "today", Starts = new DateTimeOffset(2026, 10, 2, 9, 0, 0, Amsterdam), Ends = new DateTimeOffset(2026, 10, 2, 9, 35, 0, Amsterdam) });

        // Days from today (2 Oct 2026): to 2 Nov is 31, to 2 Jan is 92, to 2 Apr is 182,
        // to 2 Oct 2027 is 365, to 2 Oct 2036 is 3653 (it passes three 29 Februaries).
        // Saturday and today count, Friday is one day too old: 70 minutes / 7 days = 10 a day.
        Assert.Equal(10 * 31, stats.Projection.OneMonthMinutes);
        Assert.Equal(10 * 92, stats.Projection.ThreeMonthsMinutes);
        Assert.Equal(10 * 182, stats.Projection.SixMonthsMinutes);
        Assert.Equal(10 * 365, stats.Projection.OneYearMinutes);
        Assert.Equal(10 * 3653, stats.Projection.TenYearsMinutes);
    }

    // Starts the app with the clock stuck at Now, stores these pomodoros, and asks /stats/pomodoros with the key.
    private static async Task<PomodoroStatsResponse> GetStatsAsync(params FeedItem[] pomodoros)
    {
        using var baseFactory = new TestApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedClock(Now))));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FeedDbContext>();
            db.FeedItems.AddRange(pomodoros);
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", TestApiFactory.ApiKey);

        return (await client.GetFromJsonAsync<PomodoroStatsResponse>("/stats/pomodoros"))!;
    }

    // A clock stuck at one moment, so "today" is the same every time the test runs.
    private class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

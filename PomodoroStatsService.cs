using Microsoft.EntityFrameworkCore;

// Your pomodoro numbers in one place, so GET /stats/pomodoros and the dashboard (section 12) can't disagree.
class PomodoroStatsService(FeedDbContext db, TimeProvider clock, TimeZoneInfo statsTimeZone)
{
    public async Task<PomodoroStatsResponse> GetAsync()
    {
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), statsTimeZone).Date;
        // Weeks start on Sunday (your call). DayOfWeek counts Sunday as 0, so a Friday (5) steps back 5 days.
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var yearStart = new DateTime(today.Year, 1, 1);

        var pomodoros = await db.FeedItems
            .Where(x => x.Source == "yapa")
            .ToListAsync();

        // Minutes from every pomodoro that started on or after firstDay, counted in Amsterdam days.
        int MinutesSince(DateTime firstDay) => (int)pomodoros
            // got rid of the clock time so we dont have to worry about the timezones messing up on what day the pomodoro started, we cant catch this error because the laptop is on amsterdam time
            .Where(x => TimeZoneInfo.ConvertTime(x.Starts, statsTimeZone).Date >= firstDay)
            .Sum(x => (x.Ends - x.Starts).TotalMinutes);

        // Your pace: average minutes a day over the last 7 days, today included.
        // Divide by 7, not by the days you worked, so a day off counts as 0.
        // 7.0, not 7: in C# a whole number divided by a whole number drops the remainder (25 / 7 is 3, not 3.57).
        var averagePerDay = MinutesSince(today.AddDays(-6)) / 7.0;

        // What that pace adds up to from today until `until`, counting real calendar days (a 31-day month, leap years).
        int ProjectedUntil(DateTime until) => (int)(averagePerDay * (until - today).TotalDays);

        return new PomodoroStatsResponse(
            MinutesSince(today),
            MinutesSince(weekStart),
            MinutesSince(monthStart),
            MinutesSince(yearStart),
            new PomodoroProjection(
                ProjectedUntil(today.AddMonths(1)),
                ProjectedUntil(today.AddMonths(3)),
                ProjectedUntil(today.AddMonths(6)),
                ProjectedUntil(today.AddYears(1)),
                ProjectedUntil(today.AddYears(10))));
    }
}

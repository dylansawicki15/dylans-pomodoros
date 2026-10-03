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

        // Your minutes for each Amsterdam day you did pomodoros on, like { 1 Oct: 25, 2 Oct: 50 }. Days off aren't in it.
        // GroupBy sorts the pomodoros into one pile per day; ToDictionary turns each pile into "day → total minutes".
        var minutesByDay = pomodoros
            // got rid of the clock time so we dont have to worry about the timezones messing up on what day the pomodoro started, we cant catch this error because the laptop is on amsterdam time
            .GroupBy(x => TimeZoneInfo.ConvertTime(x.Starts, statsTimeZone).Date)
            .ToDictionary(day => day.Key, day => (int)day.Sum(x => (x.Ends - x.Starts).TotalMinutes));

        // Minutes from every day on or after firstDay. Everything below reads minutesByDay, so which day a pomodoro counts on is decided in one place.
        int MinutesSince(DateTime firstDay) => minutesByDay
            .Where(d => d.Key >= firstDay)
            .Sum(d => d.Value);

        // Your pace on `day`: average minutes a day over that day and the 6 before it.
        // Divide by 7, not by the days you worked, so a day off counts as 0 (GetValueOrDefault gives 0 for a day that isn't in minutesByDay).
        // 7.0, not 7: in C# a whole number divided by a whole number drops the remainder (25 / 7 is 3, not 3.57).
        double PaceOn(DateTime day) => Enumerable.Range(0, 7)
            .Sum(daysBack => minutesByDay.GetValueOrDefault(day.AddDays(-daysBack))) / 7.0;

        var averagePerDay = PaceOn(today);

        // What that pace adds up to from today until `until`, counting real calendar days (a 31-day month, leap years).
        int ProjectedUntil(DateTime until) => (int)(averagePerDay * (until - today).TotalDays);

        // The pace line: your pace on every day of the last year, oldest first, today last.
        var paceLine = Enumerable.Range(0, 365)
            .Select(i => today.AddDays(i - 364))
            .Select(day => new DayPace(DateOnly.FromDateTime(day), PaceOn(day)))
            .ToList();

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
                ProjectedUntil(today.AddYears(10))),
            paceLine);
    }
}

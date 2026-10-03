using PersonalLifeApi.Components;

namespace PersonalLifeApi.Tests;

// No app, no database, no TestApiFactory: Format is a plain static method, so the test just calls it.
public class TotalFormatTests
{
    [Fact]
    public void Format_picks_minutes_hours_or_days_by_size()
    {
        Assert.Equal("25m", Total.Format(25));
        Assert.Equal("15h 50m", Total.Format(950));
        Assert.Equal("13d 21h", Total.Format(20000));
        // Exactly one day: the edge between the hours style and the days style.
        Assert.Equal("1d 0h", Total.Format(1440));
    }
}

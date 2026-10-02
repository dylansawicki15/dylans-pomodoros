using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalLifeApi.Tests;

public class StartsFilterTests
{
    // SQLite stores DateTimeOffset as text, so EF refuses to translate time comparisons: the stats filter by time in C# after ToListAsync().
    // If this ever stops throwing (e.g. a value converter to UTC ticks), the filter can move into the database.
    [Fact]
    public async Task Where_on_Starts_is_refused_by_sqlite()
    {
        using var factory = new TestApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FeedDbContext>();

        // 23:30+02:00 is 21:30 UTC: before the cutoff, but its text sorts after it.
        db.FeedItems.Add(new FeedItem { Source = "yapa", SourceId = "early", Starts = new DateTimeOffset(2026, 10, 1, 23, 30, 0, TimeSpan.FromHours(2)) });
        // 22:00 UTC: genuinely after the cutoff.
        db.FeedItems.Add(new FeedItem { Source = "yapa", SourceId = "late", Starts = new DateTimeOffset(2026, 10, 1, 22, 0, 0, TimeSpan.Zero) });
        await db.SaveChangesAsync();

        var cutoff = new DateTimeOffset(2026, 10, 1, 21, 45, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await db.FeedItems
            .Where(x => x.Starts >= cutoff)
            .ToListAsync()
        );
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalLifeApi.Tests;

public class FeedItemUniquenessTests
{
    [Fact]
    public async Task Database_rejects_a_second_row_with_the_same_source_and_source_id()
    {
        using var factory = new TestApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FeedDbContext>();

        // Skips the endpoints on purpose: this is what a buggy upsert would write.
        db.FeedItems.Add(new FeedItem { Source = "yapa", SourceId = "1" });
        db.FeedItems.Add(new FeedItem { Source = "yapa", SourceId = "1" });

        await Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }
}

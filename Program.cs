using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

string RequiredConfig(string key) =>
    builder.Configuration[key]
    ?? throw new InvalidOperationException($"This key {key} is missing. Add it to user-secrets locally or to Fly with a __ between words because fly is strange.");

var apiKey = RequiredConfig("Api:Key");
var feedConnectionString = RequiredConfig("ConnectionStrings:Feed");

builder.Services.AddOpenApi();
builder.Services.AddDbContext<FeedDbContext>(options => options.UseSqlite(feedConnectionString));
// The clock is a service so tests can swap in a fixed "now".
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var service = scope.ServiceProvider.GetRequiredService<FeedDbContext>();
    service.Database.Migrate();
}

async Task MarkSourceSucceededAsync(FeedDbContext db, string source)
{
    var sourceStatus = await db.SourceStatuses.FindAsync(source); 

    if (sourceStatus == null)
    {
        sourceStatus = new SourceStatus { Source = source }; 
        db.SourceStatuses.Add(sourceStatus);
    }

    sourceStatus.TimeStampUpdated = DateTimeOffset.UtcNow;
}

app.MapPost("/pomodoros", async (List<PomodoroPush> pomodoros, FeedDbContext db) =>
{
    foreach (var p in pomodoros)
    {
        var validPomodoro = p.Id > 0 && p.Duration > 0 && p.EndTime != default;
        if (!validPomodoro)
        {
            return Results.BadRequest($"Pomodoro {p.Id} is missing its id, duration or end time.");
        }
    }

    var added = 0;
    var updated = 0;

    var existingBySourceId = await db.FeedItems
        .Where(p => p.Source == "yapa")
        .ToDictionaryAsync(p => p.SourceId);

    foreach (var p in pomodoros)
    {
        var sourceId = p.Id.ToString();
        var ends = p.EndTime;
        var starts = ends.AddMinutes(-p.Duration);

        if (existingBySourceId.TryGetValue(sourceId, out var existing))
        {
            existing.Title = p.ProfileName;
            existing.Starts = starts;
            existing.Ends = ends;
            updated++;
        }
        else
        {

            var item = new FeedItem
            {
                Source = "yapa",
                SourceId = sourceId,
                Title = p.ProfileName,
                Starts = starts,
                Ends = ends,
            };

            db.FeedItems.Add(item);
            added++;
        }

    }

    await MarkSourceSucceededAsync(db, "yapa");
    await db.SaveChangesAsync();

    return Results.Ok(new { received = pomodoros.Count, updated, added });
})
.WithName("PushPomodoros");

app.MapGet("/feed", async (FeedDbContext db) =>
{
    var items = (await db.FeedItems.ToListAsync()) 
    .OrderBy(x => x.Starts)
    .Select(x => new FeedItemResponse(x.Source, x.SourceId, x.Title, x.Starts, x.Ends));

    return Results.Ok(items);
})
.WithName("GetFeed");

app.MapGet("/status", async (FeedDbContext db) =>
{
    var items = (await db.SourceStatuses.ToListAsync()) 
    .OrderBy(x => x.TimeStampUpdated)
    .Select(x => new SourceStatusResponse(x.Source, x.TimeStampUpdated));

    return Results.Ok(items);
})
.WithName("GetStatus");

// Days start at your midnight, not the server's (Fly runs on UTC). A named zone, not "+02:00", so the October clock change is handled.
var statsTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");

app.MapGet("/stats/pomodoros", async (FeedDbContext db, TimeProvider clock) =>
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

    return Results.Ok(new PomodoroStatsResponse(
        MinutesSince(today),
        MinutesSince(weekStart),
        MinutesSince(monthStart),
        MinutesSince(yearStart),
        new PomodoroProjection(
            ProjectedUntil(today.AddMonths(1)),
            ProjectedUntil(today.AddMonths(3)),
            ProjectedUntil(today.AddMonths(6)),
            ProjectedUntil(today.AddYears(1)),
            ProjectedUntil(today.AddYears(10)))));
})
.WithName("GetPomodoroStats");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


var publicPaths = new[] {"/health", "/", "/privacy"};
var apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);
app.Use(async (context, next) =>
{
    var providedKey = Encoding.UTF8.GetBytes(context.Request.Headers["X-API-Key"].ToString());
    if (!CryptographicOperations.FixedTimeEquals(providedKey, apiKeyBytes) && !publicPaths.Contains(context.Request.Path))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    
    await next(context);
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/health", () =>
{
    return "it is indeed a matter of fact that we are unoffline";
});

// Google requires a home page and privacy policy URL before the OAuth app can leave Testing.
app.MapGet("/", () => Results.Content(
    "<h1>Personal Life API</h1><p>We show stats of my pomodoros</p>",
    "text/html"));

app.MapGet("/privacy", () => Results.Content(
    "<h1>Personal Life API</h1><p>It reads my own pomodoro data from my computer and the YAPA program which i use to track it. The data is kept on this server on the database. I am the only one that sees the data.</p>",
    "text/html"));

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// What GET /feed promises to clients. Deliberately not FeedItem: the table's shape can change without changing the API.
record FeedItemResponse(
    string Source,
    string SourceId,
    string? Title,
    DateTimeOffset Starts,
    DateTimeOffset Ends 
);

record SourceStatusResponse(
    string Source,
    DateTimeOffset TimeStampUpdated
);

record PomodoroStatsResponse(
    int TodayMinutes,
    int WeekMinutes,
    int MonthMinutes,
    int YearMinutes,
    PomodoroProjection Projection
);

record PomodoroProjection(
    int OneMonthMinutes,
    int ThreeMonthsMinutes,
    int SixMonthsMinutes,
    int OneYearMinutes,
    int TenYearsMinutes 
);

record PomodoroPush(
    int Id,
    string? ProfileName,
    int Duration,
    DateTimeOffset EndTime
);

class FeedItem
{
    public int Id { get; set; }
    public required string Source { get; set; }
    public required string SourceId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset Starts { get; set; }
    public DateTimeOffset Ends { get; set; }
}

class SourceStatus
{
    [Key]
    public required string Source { get; set; }
    public DateTimeOffset TimeStampUpdated { get; set; }
}

class FeedDbContext : DbContext
{
    public FeedDbContext(DbContextOptions<FeedDbContext> options) : base(options) { }
    public DbSet<FeedItem> FeedItems { get; set; } = null!;
    public DbSet<SourceStatus> SourceStatuses { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // One row per item per source, enforced by the database, so a buggy upsert fails loudly instead of duplicating.
        modelBuilder.Entity<FeedItem>()
            .HasIndex(f => new { f.Source, f.SourceId }).IsUnique();
    }
}

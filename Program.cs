using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.UserSecrets;
using SQLitePCL;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;
using System.Xml.Serialization;

var builder = WebApplication.CreateBuilder(args);

string RequiredConfig(string key) =>
    builder.Configuration[key]
    ?? throw new InvalidOperationException($"This key {key} is missing. Add it to user-secrets locally or to Fly with a __ between words because fly is strange.");

var googleClientId = RequiredConfig("Google:ClientId");
var googleClientSecret = RequiredConfig("Google:ClientSecret");
var googleRefreshToken = RequiredConfig("Google:RefreshToken");
var apiKey = RequiredConfig("Api:Key");

builder.Services.AddOpenApi();
builder.Services.AddDbContext<FeedDbContext>(options => options.UseSqlite("DataSource=feed.db"));

var app = builder.Build();

const string JSON_DESERIALIZER_ERROR_MSG = "Listen brother. Whatever you just received from google isn't polished enough to be parsed by the JsonSerializer function. I think it's time for some maintenance brother.";

using var googleClient = new HttpClient();

var googleJsonOptions = new JsonSerializerOptions {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
};

var calendarJsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

async Task<GoogleTokenResponse> GetGoogleAccessTokenAsync()
{
    var form = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["client_id"] = googleClientId,
        ["client_secret"] = googleClientSecret,
        ["refresh_token"] = googleRefreshToken,
        ["grant_type"] = "refresh_token",
    });

    var response = await googleClient.PostAsync("https://oauth2.googleapis.com/token", form);

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException($"Google token exchange returned {(int)response.StatusCode}");
    }

    var rawJson = await response.Content.ReadAsStringAsync();

    var result = JsonSerializer.Deserialize<GoogleTokenResponse>(rawJson, googleJsonOptions);
    if (result == null)
    {
        throw new HttpRequestException(JSON_DESERIALIZER_ERROR_MSG);
    }
    return result;
}

async Task<GoogleEventsResponse> GetCalendarEventsRawAsync()
{
    var token = await GetGoogleAccessTokenAsync();

    var url = "https://www.googleapis.com/calendar/v3/calendars/primary/events"
        + "?timeMin=" + DateTime.UtcNow.ToString("o")
        + "&timeMax=" + DateTime.UtcNow.AddMonths(1).ToString("o")
        + "&maxResults=300"
        + "&singleEvents=true"
        + "&orderBy=startTime";

    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

    var googleData = await googleClient.SendAsync(request);
    if (!googleData.IsSuccessStatusCode)
    {
        // this is my project, and this is my error message
        throw new HttpRequestException($"Google said you done messed something up my brother, something with this number {(int)googleData.StatusCode}");
    }

    var googleRawJson = await googleData.Content.ReadAsStringAsync();

    var result = JsonSerializer.Deserialize<GoogleEventsResponse>(googleRawJson, calendarJsonOptions);
    if (result == null)
    {
        throw new HttpRequestException(JSON_DESERIALIZER_ERROR_MSG);
    }
    return result;
}

app.MapGet("/calendar/raw", async () =>
{
    try
    {
        var events = await GetCalendarEventsRawAsync();
        return Results.Ok(events);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 502);
    }
})
.WithName("GetCalendarRaw");

app.MapPost("/sync", async (FeedDbContext db) =>
{
    var events = await GetCalendarEventsRawAsync();
    var skipped = 0;
    var added = 0;
    var updated = 0;

    // Ask the database once for everything this connector has already stored,
    // keyed so the loop can look an event up without another round trip.
    var existingBySourceId = await db.FeedItems
        .Where(f => f.Source == "google")
        .ToDictionaryAsync(f => f.SourceId);

    foreach (var e in events.Items)
    {
        if (e.Start.DateTime == null || e.End.DateTime == null)
        {
            skipped++;
            continue;
        }

        if (existingBySourceId.TryGetValue(e.Id, out var existing))
        {
            existing.Title = e.Summary;
            existing.Starts = DateTimeOffset.Parse(e.Start.DateTime);
            existing.Ends = DateTimeOffset.Parse(e.End.DateTime);

            updated++;
        }
        else
        {
            var item = new FeedItem
            {
                Source = "google",
                SourceId = e.Id,
                Title = e.Summary,
                Starts = DateTimeOffset.Parse(e.Start.DateTime),
                Ends = DateTimeOffset.Parse(e.End.DateTime),
            };

            db.FeedItems.Add(item);
            added++;
        }
    }

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        fetched = events.Items.Count,
        skipped,
        updated,
        added,
        stored = await db.FeedItems.CountAsync()
    });
})
.WithName("SyncCalendar");

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var providedKey = context.Request.Headers["X-API-Key"];
    if (providedKey != apiKey)
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

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record GoogleTokenResponse(string AccessToken, double ExpiresIn);

record GoogleEventTime(string? DateTime, string? TimeZone);

record GoogleCalendarEvent(
    string Id,
    string? Summary,
    GoogleEventTime Start,
    GoogleEventTime End
);

record GoogleEventsResponse(List<GoogleCalendarEvent> Items);

// What GET /feed promises to clients. Deliberately not FeedItem: the table's shape can change without changing the API.
record FeedItemResponse(
    string Source,
    string SourceId,
    string? Title,
    DateTimeOffset Starts,
    DateTimeOffset Ends 
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

class FeedDbContext : DbContext
{
    public FeedDbContext(DbContextOptions<FeedDbContext> options) : base(options) { }
    public DbSet<FeedItem> FeedItems { get; set; } = null!;
}

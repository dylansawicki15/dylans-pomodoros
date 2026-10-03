using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PersonalLifeApi.Components;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

string RequiredConfig(string key) =>
    builder.Configuration[key]
    ?? throw new InvalidOperationException($"This key {key} is missing. Add it to user-secrets locally or to Fly with a __ between words because fly is strange.");

var apiKey = RequiredConfig("Api:Key");
var feedConnectionString = RequiredConfig("ConnectionStrings:Feed");
var googleClientId = RequiredConfig("Google:ClientId");
var googleClientSecret = RequiredConfig("Google:ClientSecret");
// The one Google account allowed into the dashboard.
var allowedEmail = RequiredConfig("Auth:AllowedEmail");

builder.Services.AddOpenApi();
builder.Services.AddDbContext<FeedDbContext>(options => options.UseSqlite(feedConnectionString));
// The clock is a service so tests can swap in a fixed "now".
builder.Services.AddSingleton(TimeProvider.System);
// Days start at your midnight, not the server's (Fly runs on UTC). A named zone, not "+02:00", so the October clock change is handled.
// Looked up here, while the app starts, so a server without time-zone data crashes at boot instead of on the first stats request.
builder.Services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"));
builder.Services.AddScoped<PomodoroStatsService>();
// Blazor: lets the app turn .razor components into HTML pages,
// and keep a live connection open to pages that ask to be interactive.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Sign-in: Google tells the app who you are once, then a cookie remembers it on every request after.
// The cookie answers "who is this?"; Google is where to send someone who hasn't signed in yet.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
    .AddCookie()
    .AddGoogle(options =>
    {
        // Read at the top, not in here: this block waits for the first request, and a missing secret should crash at boot.
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
// The key that locks the sign-in cookie. On Fly it must live on the volume: the machine's own disk is wiped on every wake-up,
// and a new key can't read old cookies, so you'd be signed out. Only Fly sets this; your laptop keeps its key in your Windows user folder.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (keysPath is not null)
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}
// "Are you allowed?" The rules [Authorize] checks against. With no rule named, the rule is: be signed in.
// "OnlyMe" is a named rule: the email Google vouched for must be yours. [Authorize(Policy = "OnlyMe")] asks for it.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OnlyMe", policy => policy.RequireClaim(ClaimTypes.Email, allowedEmail));
});
// Fly's proxy handles https and passes requests on as plain http, with a note saying how they really arrived: X-Forwarded-Proto: https.
// This makes the app believe the note, so the return address it sends Google starts with https and matches Google's list.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    // By default only a proxy on the same machine is believed. Fly's isn't, but port 8080 can only be reached through it.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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

app.MapGet("/stats/pomodoros", async (PomodoroStatsService stats) => Results.Ok(await stats.GetAsync()))
.WithName("GetPomodoroStats");

// Serves files the browser downloads as-is, like Blazor's own _framework/blazor.web.js.
app.MapStaticAssets();

// Every component with an @page line becomes a route, the same way MapGet makes one.
// The second line adds the address the live connection talks to.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();


// No key needed: pages anyone may see, and the sign-in round trip.
string[] publicPaths = ["/health", "/", "/privacy", "/signin-google", "/login", "/logout"];

// No key here either, because a browser can't keep one secret. The sign-in cookie guards these instead ([Authorize] on Dashboard.razor).
// Prefixes are whole folders: anything whose address starts with one gets in.
string[] browserPaths = ["/dashboard"];
string[] browserPrefixes = ["/_framework", "/_blazor"];

var apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);
app.Use(async (context, next) =>
{
    var providedKey = Encoding.UTF8.GetBytes(context.Request.Headers["X-API-Key"].ToString());
    var path = context.Request.Path;
    var isPublic = publicPaths.Contains(path);
    var isBrowser = browserPaths.Contains(path) || browserPrefixes.Any(prefix => path.StartsWithSegments(prefix));
    if (!CryptographicOperations.FixedTimeEquals(providedKey, apiKeyBytes) && !isPublic && !isBrowser)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    
    await next(context);
});

// Reads the sign-in cookie on every request, and takes Google's call back at /signin-google.
app.UseAuthentication();
app.UseAuthorization();

// Blazor refuses to serve pages without this. It stops other websites from submitting your forms; it matters once there's a form.
app.UseAntiforgery();

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

// Sends you to Google. Google sends you back to /signin-google with a one-time code, the app trades it
// for who you are, writes that into the cookie, and forwards you on to RedirectUri.
app.MapGet("/login", () => Results.Challenge(new AuthenticationProperties { RedirectUri = "/dashboard" }));

// Throws the wristband away: the response tells the browser to delete the sign-in cookie.
// Asking for HttpContext gets you the current request and response, the same way db gets you the database.
app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return Results.Redirect("/");
});

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

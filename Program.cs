using Microsoft.Extensions.Configuration.UserSecrets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using var habiticaClient = new HttpClient();
habiticaClient.DefaultRequestHeaders.Add("x-api-user", builder.Configuration["Habitica:UserId"]);
habiticaClient.DefaultRequestHeaders.Add("x-api-key", builder.Configuration["Habitica:ApiToken"]);
habiticaClient.DefaultRequestHeaders.Add("x-client", $"{builder.Configuration["Habitica:UserId"]}-PersonalLifeApi");

var habiticaUserData = await habiticaClient.GetAsync("https://habitica.com/api/v3/tasks/user");
var habiticaRawJson = await habiticaUserData.Content.ReadAsStringAsync();
Console.WriteLine(habiticaRawJson);

/* app.MapGet("/habitica", () =>
{
    return habiticaUserData.Content;
})
.WithName("GetHabitica"); */

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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
    // TODO(you): return a simple response showing the API is alive —
    // e.g. a plain string, or an object like new { status = "ok" }
    return "it is indeed a matter of fact that we are unoffline";
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

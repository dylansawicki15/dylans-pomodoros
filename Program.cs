using Microsoft.Extensions.Configuration.UserSecrets;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

using var habiticaClient = new HttpClient();
habiticaClient.DefaultRequestHeaders.Add("x-api-user", builder.Configuration["Habitica:UserId"]);
habiticaClient.DefaultRequestHeaders.Add("x-api-key", builder.Configuration["Habitica:ApiToken"]);
habiticaClient.DefaultRequestHeaders.Add("x-client", $"{builder.Configuration["Habitica:UserId"]}-PersonalLifeApi");

var habiticaJsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

app.MapGet("/habitica/raw", async () =>
{
    var habiticaUserData = await habiticaClient.GetAsync("https://habitica.com/api/v3/tasks/user");

    if (!habiticaUserData.IsSuccessStatusCode)
    {
        return Results.Problem(
            detail: $"Habitica returned {(int)habiticaUserData.StatusCode}", 
            statusCode: 502
        );
    }

    var habiticaRawJson = await habiticaUserData.Content.ReadAsStringAsync();
    var habiticaJson = JsonSerializer.Deserialize<HabiticaTasksResponse>(habiticaRawJson, habiticaJsonOptions);

    return Results.Ok(habiticaJson);
})
.WithName("GetHabiticaRaw");

using var googleClient = new HttpClient();

var googleJsonOptions = new JsonSerializerOptions { 
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
};

async Task<GoogleTokenResponse> GetGoogleAccessTokenAsync()
{
    var form = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["client_id"] = builder.Configuration["Google:ClientId"],
        ["client_secret"] = builder.Configuration["Google:ClientSecret"],
        ["refresh_token"] = builder.Configuration["Google:RefreshToken"],
        ["grant_type"] = "refresh_token",
    });

    var response = await googleClient.PostAsync("https://oauth2.googleapis.com/token", form);

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException($"Google token exchange returned {(int)response.StatusCode}");
    }

    var rawJson = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<GoogleTokenResponse>(rawJson, googleJsonOptions);
}

app.MapGet("/calendar/token", async () =>
{
    var token = await GetGoogleAccessTokenAsync();
    return Results.Ok(new { expiresIn = token.ExpiresIn });
})
.WithName("GetGoogleToken");

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
    return "it is indeed a matter of fact that we are unoffline";
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record HabiticaTask(string Type, string Text);
record HabiticaTasksResponse(bool Success, List<HabiticaTask> Data);

record GoogleTokenResponse(string AccessToken, double ExpiresIn);
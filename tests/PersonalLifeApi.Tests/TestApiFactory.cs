using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace PersonalLifeApi.Tests;

// The real app, booted in memory with fake settings and its own throwaway database.
// Every test gets its app from here, so a new required setting is added in one place.
public class TestApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-key";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"personal-life-api-test-{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Api:Key", ApiKey);
        builder.UseSetting("ConnectionStrings:Feed", $"DataSource={_dbPath}");
        // this is a professional project, clearly
        builder.UseSetting("Google:ClientId", "agoobadoobeedoo");
        builder.UseSetting("Google:ClientSecret", "laboobeeboobeeboo");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // SQLite keeps closed connections pooled, and Windows refuses to delete a file that is still open.
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }
}

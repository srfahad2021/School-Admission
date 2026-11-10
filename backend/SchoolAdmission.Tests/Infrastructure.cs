using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using System.Net.Http.Json;
namespace SchoolAdmission.Tests;

// One Postgres container for the whole test run (cleaned up automatically when tests finish).
public static class SharedPostgres
{
    private static readonly Lazy<Task<string>> Cs = new(async () =>
    {
        var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithCommand("-c", "max_connections=500")
            .Build();
        await container.StartAsync();
        return container.GetConnectionString();
    });

    public static Task<string> ConnectionString => Cs.Value;
}

// Boots the real API in memory against its own fresh database.
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly int _holdSeconds;

    private ApiFactory(string connectionString, int holdSeconds)
    {
        _connectionString = connectionString;
        _holdSeconds = holdSeconds;
    }

    public static async Task<ApiFactory> CreateAsync(int holdSeconds = 600, string? database = null)
    {
        var baseCs = await SharedPostgres.ConnectionString;
        var csb = new NpgsqlConnectionStringBuilder(baseCs)
        {
            Database = database ?? "t_" + Guid.NewGuid().ToString("N"),
            MaxPoolSize = 50
        };
        return new ApiFactory(csb.ConnectionString, holdSeconds);
    }
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"] = "Warning",
                ["HOLD_DURATION_SECONDS"] = _holdSeconds.ToString()
            }));
    }
}

public static class Api
{
    // Always exactly 17 digits.
    public static string StudentId(int n) => (10_000_000_000_000_000L + n).ToString();

    public static HttpRequestMessage Req(HttpMethod m, string url, string? studentId = null)
    {
        var r = new HttpRequestMessage(m, url);
        if (studentId != null) r.Headers.Add("X-Student-Id", studentId);
        return r;
    }

    public static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public static async Task<string> CreateClass(HttpClient c, int seats)
    {
        var r = await c.PostAsJsonAsync("/classes", new { name = "Test Class", totalSeats = seats });
        r.EnsureSuccessStatusCode();
        return (await Json(r)).GetProperty("id").GetString()!;
    }

    public static Task<HttpResponseMessage> Apply(HttpClient c, string classId, string studentId) =>
        c.SendAsync(Req(HttpMethod.Post, $"/classes/{classId}/holds", studentId));

    public static Task<HttpResponseMessage> Confirm(HttpClient c, string holdId, string studentId) =>
        c.SendAsync(Req(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId));

    public static async Task<JsonElement> GetClass(HttpClient c, string classId) =>
        await Json(await c.GetAsync($"/classes/{classId}"));
}
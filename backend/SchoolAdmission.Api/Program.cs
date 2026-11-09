using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings__Default is not set.");

var holdDurationSeconds = int.TryParse(
    Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS"), out var s) ? s : 600;

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

await WaitForDatabaseAsync(connectionString);

app.MapGet("/health", () => Results.Ok(new { status = "ok", holdDurationSeconds }));

app.Run();

static async Task WaitForDatabaseAsync(string cs)
{
    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            Console.WriteLine("Database connection OK.");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database not ready ({attempt}/30): {ex.Message}");
            await Task.Delay(2000);
        }
    }
    throw new Exception("Could not connect to the database.");
}
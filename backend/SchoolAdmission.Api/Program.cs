using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings__Default is not set.");

var holdSeconds = int.TryParse(builder.Configuration["HOLD_DURATION_SECONDS"], out var s) && s > 0 ? s : 600;

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(new HoldOptions(holdSeconds));
builder.Services.AddScoped<AdmissionService>();
builder.Services.AddHostedService<ExpiryService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

// Turn every error into friendly JSON: { "error": "..." }
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex) when (!ctx.Response.HasStarted)
    {
        var (status, msg) = ex switch
        {
            ApiException a => (a.Status, a.Message),
            BadHttpRequestException => (400, "The request was not valid. Please check the values you entered."),
            _ => (500, "Something went wrong on the server. Please try again.")
        };
        if (status == 500)
            app.Logger.LogError(ex, "Unhandled error");
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { error = msg });
    }
});

await InitDatabaseAsync(app.Services, app.Logger);

app.MapGet("/health", () => Results.Ok(new { status = "ok", holdDurationSeconds = holdSeconds }));

// 1. Create class
app.MapPost("/classes", async (CreateClassRequest req, AdmissionService svc) =>
{
    var c = await svc.CreateClassAsync(req.Name, req.TotalSeats);
    return Results.Created($"/classes/{c.Id}", new { id = c.Id, name = c.Name, totalSeats = c.TotalSeats });
});

// 2. List classes
app.MapGet("/classes", async (AdmissionService svc) => Results.Ok(await svc.ListClassesAsync()));

// 3. Get class
app.MapGet("/classes/{classId}", async (string classId, AdmissionService svc) =>
    Results.Ok(await svc.GetClassAsync(classId)));

// 4. Apply for hold
app.MapPost("/classes/{classId}/holds", async (string classId, HttpRequest req, AdmissionService svc) =>
{
    var studentId = RequireStudentId(req);
    var h = await svc.ApplyAsync(classId, studentId);
    return Results.Created($"/holds/{h.Id}", new
    {
        holdId = h.Id, classId = h.ClassId, studentId = h.StudentId,
        status = h.Status, expiresAt = h.ExpiresAt
    });
});

// 5. View hold
app.MapGet("/holds/{holdId}", async (string holdId, AdmissionService svc) =>
{
    var (h, status) = await svc.GetHoldAsync(holdId);
    return Results.Ok(new { holdId = h.Id, classId = h.ClassId, studentId = h.StudentId, status, expiresAt = h.ExpiresAt });
});

// 6. Confirm
app.MapPost("/holds/{holdId}/confirm", async (string holdId, HttpRequest req, AdmissionService svc) =>
{
    var h = await svc.ConfirmAsync(holdId, RequireStudentId(req));
    return Results.Ok(new { holdId = h.Id, status = h.Status });
});

// 7. Cancel
app.MapDelete("/holds/{holdId}", async (string holdId, HttpRequest req, AdmissionService svc) =>
{
    await svc.CancelAsync(holdId, RequireStudentId(req));
    return Results.NoContent();
});

// 8. My applications
app.MapGet("/me/holds", async (HttpRequest req, AdmissionService svc) =>
    Results.Ok(await svc.MyHoldsAsync(RequireStudentId(req))));

app.Run();

static string RequireStudentId(HttpRequest req)
{
    var v = req.Headers["X-Student-Id"].ToString().Trim();
    if (v.Length != 17 || !v.All(char.IsAsciiDigit))
        throw new ApiException(400, "Student ID (Birth Registration Number) must be exactly 17 digits.");
    return v;
}

static async Task InitDatabaseAsync(IServiceProvider sp, ILogger log)
{
    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            using var scope = sp.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            log.LogInformation("Database ready.");
            return;
        }
        catch (Exception ex)
        {
            log.LogWarning("Database not ready ({Attempt}/30): {Msg}", attempt, ex.Message);
            await Task.Delay(2000);
        }
    }
    throw new Exception("Could not initialise the database.");
}

public partial class Program { }   // needed later so tests can start the app
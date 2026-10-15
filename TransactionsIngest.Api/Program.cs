using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Services;
using TransactionsIngest.Api.SourceSystems;

var builder = WebApplication.CreateBuilder(args);

// --- Logging ---
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// --- Configuration (externalized via appsettings.json / appsettings.{Environment}.json / env vars) ---
var connectionString = builder.Configuration.GetConnectionString("TransactionsDb")
    ?? "Data Source=transactions.db";

// --- EF Core / SQLite, Code-First ---
builder.Services.AddDbContext<TransactionsDbContext>(options =>
    options.UseSqlite(connectionString));

// --- Clock abstraction: lets tests inject a fixed point in time instead of depending on the
//     real system clock, which is what makes the 24h revocation/finalization logic testable. ---
builder.Services.AddSingleton(TimeProvider.System);

// --- Source-system clients: three registrations of the same generic class, one per mock JSON
//     file. See JsonFileSourceSystemClient for why this is a single reusable class rather than
//     three near-identical ones. ---
var mockDataRoot = Path.Combine(builder.Environment.ContentRootPath, "MockData");
builder.Services.AddSingleton<ISourceSystemClient>(_ =>
    new JsonFileSourceSystemClient("Store Uptown", Path.Combine(mockDataRoot, "store-uptown.json")));
builder.Services.AddSingleton<ISourceSystemClient>(_ =>
    new JsonFileSourceSystemClient("Store Downtown", Path.Combine(mockDataRoot, "store-downtown.json")));
builder.Services.AddSingleton<ISourceSystemClient>(_ =>
    new JsonFileSourceSystemClient("Online", Path.Combine(mockDataRoot, "online.json")));

builder.Services.AddScoped<ITransactionReconciliationService, TransactionReconciliationService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CORS: the Angular dev server runs on a different port (4200) than the API, so the browser
//     will block calls without an explicit CORS policy during local development. ---
const string AngularDevCorsPolicy = "AngularDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// --- Apply EF Core migrations automatically on startup so `dotnet run` alone is enough to get a
//     working SQLite database - no separate manual migration step needed to try the app. ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- Minimal global error handler: anything that escapes a controller's own try/catch is logged
//     and turned into a generic 500 response instead of leaking a stack trace to the client. ---
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (feature?.Error is { } ex)
        {
            logger.LogError(ex, "Unhandled exception");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

app.UseCors(AngularDevCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }

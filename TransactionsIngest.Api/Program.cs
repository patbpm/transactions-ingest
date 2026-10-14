using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TransactionsDb")
    ?? "Data Source=transactions.db";

builder.Services.AddDbContext<TransactionsDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }

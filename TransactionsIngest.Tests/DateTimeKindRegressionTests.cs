using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Models;
using TransactionsIngest.Api.Services;
using Xunit;

namespace TransactionsIngest.Tests;

/// <summary>
/// Regression test for a timestamp bug that only shows up across separate DbContext instances.
/// SQLite returns stored DateTime values with Kind=Unspecified, while the source clients produce
/// Kind=Utc. Within one DbContext the tracked entity keeps its original Kind, so tests that share a
/// context never see the difference. In production every HTTP request gets a fresh context, so a
/// second import compared an Unspecified value to a Utc value, saw a "change", and wrote a bogus
/// TransactionTime update. This test uses a new context (and service) per import to cover that path.
/// </summary>
public class DateTimeKindRegressionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TransactionsDbContext> _options;

    public DateTimeKindRegressionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<TransactionsDbContext>().UseSqlite(_connection).Options;

        using var setup = new TransactionsDbContext(_options);
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static async Task<ImportResultDto> ImportWithFreshContextAsync(
        DbContextOptions<TransactionsDbContext> options,
        ManualTimeProvider time,
        FakeSourceSystemClient client)
    {
        await using var db = new TransactionsDbContext(options);
        var service = new TransactionReconciliationService(
            db, new[] { client }, NullLogger<TransactionReconciliationService>.Instance, time);
        return await service.RunImportAsync(ImportTrigger.Manual);
    }

    [Fact]
    public async Task Reimporting_unchanged_data_with_a_fresh_DbContext_reports_no_changes()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var client = new FakeSourceSystemClient("Online");
        client.Snapshot.Add(new SourceTransactionDto
        {
            SourceSystem = "Online",
            TransactionId = 1,
            CardNumber = "***1234",
            LocationCode = "ONLINE",
            ProductName = "Widget",
            Amount = 10.00m,
            TransactionTime = DateTime.SpecifyKind(new DateTime(2026, 1, 1, 11, 0, 0), DateTimeKind.Utc)
        });

        var first = await ImportWithFreshContextAsync(_options, time, client);
        var second = await ImportWithFreshContextAsync(_options, time, client);

        Assert.Equal(1, first.NewCount);
        Assert.Equal(0, second.NewCount);
        Assert.Equal(0, second.UpdatedCount);
        Assert.Equal(1, second.UnchangedCount);

        await using var verify = new TransactionsDbContext(_options);
        Assert.Equal(1, await verify.AuditEntries.CountAsync());
        Assert.Equal(0, (await verify.Transactions.SingleAsync()).RevisionNumber);
    }
}

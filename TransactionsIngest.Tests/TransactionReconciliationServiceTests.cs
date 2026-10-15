using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Models;
using TransactionsIngest.Api.Services;
using Xunit;

namespace TransactionsIngest.Tests;

/// <summary>
/// Uses the real SQLite provider against an in-memory, open-connection database per test
/// (rather than EF Core's separate "InMemory" provider) so the tests exercise the same
/// provider, migrations, and constraints (like the unique index) that run in production.
/// </summary>
public class TransactionReconciliationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TransactionsDbContext _db;

    public TransactionReconciliationServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TransactionsDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private TransactionReconciliationService CreateService(
        ManualTimeProvider timeProvider,
        params FakeSourceSystemClient[] clients)
    {
        return new TransactionReconciliationService(_db, clients, NullLogger<TransactionReconciliationService>.Instance, timeProvider);
    }

    private static SourceTransactionDto MakeDto(string source, int id, string product, decimal amount, DateTime time)
        => new()
        {
            SourceSystem = source,
            TransactionId = id,
            CardNumber = "***1234",
            LocationCode = "LOC",
            ProductName = product,
            Amount = amount,
            TransactionTime = time
        };

    [Fact]
    public async Task New_transaction_is_inserted_and_audited_as_Created()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var client = new FakeSourceSystemClient("Online");
        client.Snapshot.Add(MakeDto("Online", 1, "Widget", 10.00m, time.GetUtcNow().UtcDateTime));

        var service = CreateService(time, client);
        var result = await service.RunImportAsync(ImportTrigger.Manual);

        Assert.Equal(1, result.NewCount);
        var record = await _db.Transactions.SingleAsync();
        Assert.Equal(TransactionStatus.Active, record.Status);
        Assert.Equal(0, record.RevisionNumber);

        var audit = await _db.AuditEntries.SingleAsync();
        Assert.Equal(AuditChangeType.Created, audit.ChangeType);
    }

    [Fact]
    public async Task Changed_field_is_detected_updates_the_record_and_writes_an_audit_entry()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var client = new FakeSourceSystemClient("Online");
        client.Snapshot.Add(MakeDto("Online", 1, "Widget", 10.00m, time.GetUtcNow().UtcDateTime));
        var service = CreateService(time, client);
        await service.RunImportAsync(ImportTrigger.Manual);

        // Source publishes a price correction on the same transaction.
        client.Snapshot[0].Amount = 12.50m;
        time.Advance(TimeSpan.FromMinutes(5));
        var result = await service.RunImportAsync(ImportTrigger.Manual);

        Assert.Equal(0, result.NewCount);
        Assert.Equal(1, result.UpdatedCount);

        var record = await _db.Transactions.SingleAsync();
        Assert.Equal(12.50m, record.Amount);
        Assert.Equal(1, record.RevisionNumber);

        var fieldChange = await _db.AuditEntries.SingleAsync(a => a.ChangeType == AuditChangeType.FieldChanged);
        Assert.Equal(nameof(TransactionRecord.Amount), fieldChange.FieldName);
        Assert.Equal("10.00", fieldChange.OldValue);
        Assert.Equal("12.50", fieldChange.NewValue);
    }

    [Fact]
    public async Task Transaction_missing_within_24h_of_TransactionTime_is_revoked()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var client = new FakeSourceSystemClient("Online");
        client.Snapshot.Add(MakeDto("Online", 1, "Widget", 10.00m, time.GetUtcNow().UtcDateTime));
        var service = CreateService(time, client);
        await service.RunImportAsync(ImportTrigger.Manual);

        // Source pulls the transaction a couple of hours later - still well within the 24h window.
        client.Snapshot.Clear();
        time.Advance(TimeSpan.FromHours(2));
        var result = await service.RunImportAsync(ImportTrigger.Manual);

        Assert.Equal(1, result.RevokedCount);
        var record = await _db.Transactions.SingleAsync();
        Assert.Equal(TransactionStatus.Revoked, record.Status);
        Assert.Contains(await _db.AuditEntries.ToListAsync(), a => a.ChangeType == AuditChangeType.Revoked);
    }

    [Fact]
    public async Task Transaction_missing_after_24h_of_TransactionTime_is_NOT_revoked()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var client = new FakeSourceSystemClient("Online");
        client.Snapshot.Add(MakeDto("Online", 1, "Widget", 10.00m, time.GetUtcNow().UtcDateTime));
        var service = CreateService(time, client);
        await service.RunImportAsync(ImportTrigger.Manual);

        // First, let it age past 24h while still present, so it gets Finalized rather than Revoked.
        time.Advance(TimeSpan.FromHours(25));
        await service.RunImportAsync(ImportTrigger.Manual);

        var finalized = await _db.Transactions.SingleAsync();
        Assert.Equal(TransactionStatus.Finalized, finalized.Status);

        // Now it disappears from the snapshot entirely - should NOT flip back to Revoked,
        // because it is already old/settled, not "unexpectedly pulled while pending".
        client.Snapshot.Clear();
        time.Advance(TimeSpan.FromHours(1));
        var result = await service.RunImportAsync(ImportTrigger.Manual);

        Assert.Equal(0, result.RevokedCount);
        var record = await _db.Transactions.SingleAsync();
        Assert.Equal(TransactionStatus.Finalized, record.Status);
    }

    [Fact]
    public async Task Running_the_same_import_twice_with_identical_data_is_idempotent()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var client = new FakeSourceSystemClient("Online");
        client.Snapshot.Add(MakeDto("Online", 1, "Widget", 10.00m, time.GetUtcNow().UtcDateTime));
        client.Snapshot.Add(MakeDto("Online", 2, "Gadget", 20.00m, time.GetUtcNow().UtcDateTime));
        var service = CreateService(time, client);

        var first = await service.RunImportAsync(ImportTrigger.Manual);
        Assert.Equal(2, first.NewCount);

        time.Advance(TimeSpan.FromMinutes(10));
        var second = await service.RunImportAsync(ImportTrigger.Manual);

        Assert.Equal(0, second.NewCount);
        Assert.Equal(0, second.UpdatedCount);
        Assert.Equal(0, second.RevokedCount);
        Assert.Equal(2, second.UnchangedCount);

        // Still exactly 2 Created audit entries total - the second run added none.
        var createdCount = await _db.AuditEntries.CountAsync(a => a.ChangeType == AuditChangeType.Created);
        Assert.Equal(2, createdCount);

        var records = await _db.Transactions.ToListAsync();
        Assert.All(records, r => Assert.Equal(0, r.RevisionNumber));
    }

    [Fact]
    public async Task Transaction_ids_that_collide_across_source_systems_are_kept_separate()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var uptown = new FakeSourceSystemClient("Store Uptown");
        var online = new FakeSourceSystemClient("Online");
        uptown.Snapshot.Add(MakeDto("Store Uptown", 100001, "Mouse", 24.99m, time.GetUtcNow().UtcDateTime));
        online.Snapshot.Add(MakeDto("Online", 100001, "Dock", 129.95m, time.GetUtcNow().UtcDateTime));

        var service = CreateService(time, uptown, online);
        var result = await service.RunImportAsync(ImportTrigger.Manual);

        Assert.Equal(2, result.NewCount);
        var records = await _db.Transactions.Where(t => t.TransactionId == 100001).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.Contains(records, r => r.SourceSystem == "Store Uptown" && r.ProductName == "Mouse");
        Assert.Contains(records, r => r.SourceSystem == "Online" && r.ProductName == "Dock");
    }
}

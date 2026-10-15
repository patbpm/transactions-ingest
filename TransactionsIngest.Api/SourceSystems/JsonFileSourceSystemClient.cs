using System.Text.Json;
using TransactionsIngest.Api.Models;

namespace TransactionsIngest.Api.SourceSystems;

/// <summary>
/// Shape of one row in a mock source-system JSON file. Deliberately uses "minutesAgo" instead of
/// an absolute TransactionTime: a coding exercise's mock data is usually reviewed days after it's
/// written, and a fixed date baked in today would be stale (and past the 24h revocation/finalize
/// window) by the time anyone runs it.
/// </summary>
internal class MockTransactionFileRow
{
    public int TransactionId { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int MinutesAgo { get; set; }
}

/// <summary>
/// A single, reusable mock client that reads its "live" snapshot from a JSON file on disk
/// (under MockData/) instead of hard-coding three near-identical classes. One instance of this
/// class is registered per source system, each pointed at its own file - adding a 4th source
/// system later means adding a JSON file and one registration line in Program.cs, not a new class
/// (Open/Closed Principle).
///
/// Editing the JSON file between calls to POST /api/import is how you manually simulate a source
/// system publishing a correction (change amount/product/etc.), a delayed transaction (add a new
/// entry), or a removal (delete an entry).
///
/// Timestamp resolution: "minutesAgo" is resolved against the mock FILE'S OWN last-write time
/// (File.GetLastWriteTimeUtc), not "DateTime.UtcNow at request time". This is deliberate: if it
/// were resolved against the request-time clock, TransactionTime would silently drift forward by
/// a few minutes on every single import run, even with a completely untouched file - which would
/// make every re-run look like a field change and break idempotency. Anchoring on the file's mtime
/// means re-running the import against an unmodified file always resolves to the exact same
/// timestamps (true idempotency), while editing the file to run a demo naturally produces fresh,
/// recent timestamps again.
/// </summary>
public class JsonFileSourceSystemClient : ISourceSystemClient
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonFileSourceSystemClient(string sourceSystemName, string filePath)
    {
        SourceSystemName = sourceSystemName;
        _filePath = filePath;
    }

    public string SourceSystemName { get; }

    public async Task<IReadOnlyList<SourceTransactionDto>> GetTransactionSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            // Treat a missing file as "source system returned an empty snapshot" rather than
            // throwing - makes it easy to demo full revocation by deleting a mock file entirely.
            return Array.Empty<SourceTransactionDto>();
        }

        var anchorUtc = File.GetLastWriteTimeUtc(_filePath);

        await using var stream = File.OpenRead(_filePath);
        var rows = await JsonSerializer.DeserializeAsync<List<MockTransactionFileRow>>(stream, JsonOptions, cancellationToken)
                   ?? new List<MockTransactionFileRow>();

        return rows.Select(row => new SourceTransactionDto
        {
            SourceSystem = SourceSystemName,
            TransactionId = row.TransactionId,
            CardNumber = row.CardNumber,
            LocationCode = row.LocationCode,
            ProductName = row.ProductName,
            Amount = row.Amount,
            TransactionTime = anchorUtc - TimeSpan.FromMinutes(row.MinutesAgo)
        }).ToList();
    }
}

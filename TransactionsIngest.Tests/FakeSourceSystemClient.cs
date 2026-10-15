using TransactionsIngest.Api.Models;
using TransactionsIngest.Api.SourceSystems;

namespace TransactionsIngest.Tests;

/// <summary>
/// A test double for ISourceSystemClient: returns whatever snapshot the test configures,
/// instead of reading from a JSON file. This is the payoff of coding against an interface -
/// the reconciliation service never knows it's talking to a fake in tests vs. the real
/// JsonFileSourceSystemClient in production.
/// </summary>
public class FakeSourceSystemClient : ISourceSystemClient
{
    public string SourceSystemName { get; }
    public List<SourceTransactionDto> Snapshot { get; set; } = new();

    public FakeSourceSystemClient(string sourceSystemName)
    {
        SourceSystemName = sourceSystemName;
    }

    public Task<IReadOnlyList<SourceTransactionDto>> GetTransactionSnapshotAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SourceTransactionDto>>(Snapshot);
}

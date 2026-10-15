using TransactionsIngest.Api.Models;

namespace TransactionsIngest.Api.SourceSystems;

/// <summary>
/// Abstraction over "call an external source system and get back its current transaction snapshot."
/// In production this would wrap an HTTP client calling a real API. For this exercise every
/// implementation is a mock, but the import engine (TransactionReconciliationService) only ever
/// depends on this interface - so swapping a mock for a real HTTP-backed client later requires
/// zero changes to the reconciliation logic. This is the Dependency Inversion Principle (the "D"
/// in SOLID) in practice.
/// </summary>
public interface ISourceSystemClient
{
    string SourceSystemName { get; }

    Task<IReadOnlyList<SourceTransactionDto>> GetTransactionSnapshotAsync(CancellationToken cancellationToken = default);
}

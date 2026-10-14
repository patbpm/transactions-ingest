namespace TransactionsIngest.Api.Models;

/// <summary>
/// Lifecycle status of an imported transaction.
/// Active      -> currently present in the latest source snapshot (or within its 24h grace window).
/// Revoked     -> was previously seen, disappeared from the source snapshot within 24h of its TransactionTime.
/// Finalized   -> older than 24h; treated as settled/immutable and no longer subject to revocation or field updates.
/// </summary>
public enum TransactionStatus
{
    Active = 0,
    Revoked = 1,
    Finalized = 2
}

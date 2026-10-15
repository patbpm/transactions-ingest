namespace TransactionsIngest.Api.Models;

/// <summary>Row shape returned by GET /api/transactions - only what the frontend table needs.</summary>
public record TransactionListItemDto(
    int Id,
    string SourceSystem,
    int TransactionId,
    string LocationCode,
    string ProductName,
    decimal Amount,
    DateTime TransactionTime,
    string Status,
    int RevisionNumber
);

/// <summary>Row shape returned by GET /api/transactions/{id}/audit.</summary>
public record AuditEntryDto(
    int Id,
    string ChangeType,
    string? FieldName,
    string? OldValue,
    string? NewValue,
    DateTime ChangedAtUtc,
    int ImportBatchId
);

/// <summary>Response shape for POST /api/import - a summary so the caller (or frontend button) knows what happened.</summary>
public record ImportResultDto(
    int ImportBatchId,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    int NewCount,
    int UpdatedCount,
    int RevokedCount,
    int FinalizedCount,
    int UnchangedCount
);

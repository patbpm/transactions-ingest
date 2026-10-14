using System.ComponentModel.DataAnnotations;

namespace TransactionsIngest.Api.Models;

public enum AuditChangeType
{
    Created = 0,
    FieldChanged = 1,
    Revoked = 2,
    Reinstated = 3,
    Finalized = 4
}

/// <summary>
/// One row per detected change. For a FieldChanged event we write one row per changed field,
/// so the audit trail reads like a diff rather than an opaque "something changed".
/// </summary>
public class TransactionAuditEntry
{
    [Key]
    public int Id { get; set; }

    public int TransactionRecordId { get; set; }
    public TransactionRecord? TransactionRecord { get; set; }

    public int ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }

    public AuditChangeType ChangeType { get; set; }

    [MaxLength(50)]
    public string? FieldName { get; set; }

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public DateTime ChangedAtUtc { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransactionsIngest.Api.Models;

/// <summary>
/// The current, reconciled state of one transaction. One row per logical transaction,
/// keyed by (SourceSystem, TransactionId). Updated in place as new snapshots arrive;
/// every detected change is also recorded as a separate TransactionAuditEntry row.
/// </summary>
public class TransactionRecord
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>Unique only within SourceSystem - NOT a global unique identifier on its own.</summary>
    public int TransactionId { get; set; }

    [MaxLength(20)]
    public string CardNumber { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string LocationCode { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime TransactionTime { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    /// <summary>Incremented each time any field value changes after the initial insert.</summary>
    public int RevisionNumber { get; set; } = 0;

    public DateTime FirstSeenUtc { get; set; }

    /// <summary>Last time this transaction was present in a source snapshot. Used for revocation detection.</summary>
    public DateTime LastSeenUtc { get; set; }

    public int? LastImportBatchId { get; set; }
    public ImportBatch? LastImportBatch { get; set; }

    public ICollection<TransactionAuditEntry> AuditEntries { get; set; } = new List<TransactionAuditEntry>();
}

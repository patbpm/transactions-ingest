using System.ComponentModel.DataAnnotations;

namespace TransactionsIngest.Api.Models;

public enum ImportTrigger
{
    Manual = 0,
    Scheduled = 1
}

/// <summary>
/// One row per import run. Gives every audit entry a parent "batch" to reference,
/// and gives the API something useful to return immediately after POST /api/import.
/// </summary>
public class ImportBatch
{
    [Key]
    public int Id { get; set; }

    public ImportTrigger Trigger { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public int RevokedCount { get; set; }
    public int FinalizedCount { get; set; }
    public int UnchangedCount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Models;
using TransactionsIngest.Api.SourceSystems;

namespace TransactionsIngest.Api.Services;

/// <summary>
/// The core of the exercise: reconciles each source system's current snapshot against what's
/// already in the database, and records every detected change as an audit entry.
///
/// Design decision (documented in README too): this is comparison-based reconciliation, not
/// event-based. Each run re-derives the full current state by diffing "what the source says now"
/// against "what we last stored" - it does not replay a stream of events. That is what makes the
/// whole process naturally idempotent: running it twice with identical source data produces zero
/// additional audit entries and zero revision bumps on the second run, because there is nothing
/// left to diff.
/// </summary>
public class TransactionReconciliationService : ITransactionReconciliationService
{
    private static readonly TimeSpan RevocationWindow = TimeSpan.FromHours(24);

    private readonly TransactionsDbContext _db;
    private readonly IEnumerable<ISourceSystemClient> _sourceClients;
    private readonly ILogger<TransactionReconciliationService> _logger;
    private readonly TimeProvider _timeProvider;

    public TransactionReconciliationService(
        TransactionsDbContext db,
        IEnumerable<ISourceSystemClient> sourceClients,
        ILogger<TransactionReconciliationService> logger,
        TimeProvider timeProvider)
    {
        _db = db;
        _sourceClients = sourceClients;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<ImportResultDto> RunImportAsync(ImportTrigger trigger, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var batch = new ImportBatch
        {
            Trigger = trigger,
            StartedAtUtc = now
        };
        _db.ImportBatches.Add(batch);
        // Save immediately so batch.Id exists and can be referenced by audit entries created below.
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Import batch {BatchId} started ({Trigger})", batch.Id, trigger);

        // Wrap the whole batch in one DB transaction: either every source system's changes land
        // together, or (on an unhandled error) none of them do. Avoids a half-applied import.
        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var client in _sourceClients)
            {
                await ReconcileSourceAsync(client, batch, now, cancellationToken);
            }

            batch.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import batch {BatchId} failed; rolling back", batch.Id);
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }

        _logger.LogInformation(
            "Import batch {BatchId} completed: {New} new, {Updated} updated, {Revoked} revoked, {Finalized} finalized, {Unchanged} unchanged",
            batch.Id, batch.NewCount, batch.UpdatedCount, batch.RevokedCount, batch.FinalizedCount, batch.UnchangedCount);

        return new ImportResultDto(
            batch.Id,
            batch.StartedAtUtc,
            batch.CompletedAtUtc ?? now,
            batch.NewCount,
            batch.UpdatedCount,
            batch.RevokedCount,
            batch.FinalizedCount,
            batch.UnchangedCount);
    }

    private async Task ReconcileSourceAsync(
        ISourceSystemClient client,
        ImportBatch batch,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var sourceName = client.SourceSystemName;
        IReadOnlyList<Models.SourceTransactionDto> snapshot;

        try
        {
            snapshot = await client.GetTransactionSnapshotAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // A single source system failing to respond should not take down the whole import run -
            // log it and move on to the next source. (In production this would also feed a
            // per-source health/alerting signal.)
            _logger.LogError(ex, "Source system {SourceSystem} failed to return a snapshot; skipping it this run", sourceName);
            return;
        }

        var existingRecords = await _db.Transactions
            .Where(t => t.SourceSystem == sourceName)
            .ToDictionaryAsync(t => t.TransactionId, cancellationToken);

        var seenTransactionIds = new HashSet<int>();

        foreach (var dto in snapshot)
        {
            seenTransactionIds.Add(dto.TransactionId);

            if (!existingRecords.TryGetValue(dto.TransactionId, out var record))
            {
                record = new TransactionRecord
                {
                    SourceSystem = sourceName,
                    TransactionId = dto.TransactionId,
                    CardNumber = dto.CardNumber,
                    LocationCode = dto.LocationCode,
                    ProductName = dto.ProductName,
                    Amount = dto.Amount,
                    TransactionTime = dto.TransactionTime,
                    Status = TransactionStatus.Active,
                    RevisionNumber = 0,
                    FirstSeenUtc = now,
                    LastSeenUtc = now,
                    LastImportBatchId = batch.Id
                };
                _db.Transactions.Add(record);

                _db.AuditEntries.Add(new TransactionAuditEntry
                {
                    TransactionRecord = record,
                    ImportBatchId = batch.Id,
                    ChangeType = AuditChangeType.Created,
                    ChangedAtUtc = now
                });

                batch.NewCount++;
                continue;
            }

            // Existing record. Finalized transactions are immutable by design - we still note that
            // we saw it (so it is correctly excluded from the revocation pass below) but we do not
            // compare or overwrite its fields.
            record.LastSeenUtc = now;
            record.LastImportBatchId = batch.Id;

            if (record.Status == TransactionStatus.Finalized)
            {
                continue;
            }

            var wasRevoked = record.Status == TransactionStatus.Revoked;
            var fieldsChanged = CompareAndRecordFieldChanges(record, dto, batch, now);

            if (wasRevoked)
            {
                record.Status = TransactionStatus.Active;
                _db.AuditEntries.Add(new TransactionAuditEntry
                {
                    TransactionRecord = record,
                    ImportBatchId = batch.Id,
                    ChangeType = AuditChangeType.Reinstated,
                    ChangedAtUtc = now
                });
                batch.UpdatedCount++;
            }
            else if (fieldsChanged)
            {
                record.RevisionNumber++;
                batch.UpdatedCount++;
            }
            else
            {
                batch.UnchangedCount++;
            }
        }

        // Revocation + finalization pass over every Active record for this source.
        foreach (var record in existingRecords.Values.Where(r => r.Status == TransactionStatus.Active))
        {
            var age = now - record.TransactionTime;
            var wasSeenThisRun = seenTransactionIds.Contains(record.TransactionId);

            if (!wasSeenThisRun)
            {
                if (age <= RevocationWindow)
                {
                    record.Status = TransactionStatus.Revoked;
                    _db.AuditEntries.Add(new TransactionAuditEntry
                    {
                        TransactionRecord = record,
                        ImportBatchId = batch.Id,
                        ChangeType = AuditChangeType.Revoked,
                        ChangedAtUtc = now
                    });
                    batch.RevokedCount++;
                }
                // else: older than 24h and simply absent from this snapshot - treated as an
                // expected part of the source system's normal data lifecycle, not a revocation
                // signal. See README "Design decisions and assumptions".
            }
            else if (age > RevocationWindow)
            {
                // Present and confirmed, but now old enough to be considered settled: finalize it.
                record.Status = TransactionStatus.Finalized;
                _db.AuditEntries.Add(new TransactionAuditEntry
                {
                    TransactionRecord = record,
                    ImportBatchId = batch.Id,
                    ChangeType = AuditChangeType.Finalized,
                    ChangedAtUtc = now
                });
                batch.FinalizedCount++;
            }
        }
    }

    /// <summary>
    /// Compares every mutable field between the stored record and the incoming snapshot row,
    /// writing one audit entry per changed field. Returns true if anything changed.
    /// </summary>
    private bool CompareAndRecordFieldChanges(
        TransactionRecord record,
        Models.SourceTransactionDto dto,
        ImportBatch batch,
        DateTime now)
    {
        var changed = false;

        changed |= RecordIfChanged(record, batch, now, nameof(record.CardNumber), record.CardNumber, dto.CardNumber,
            () => record.CardNumber = dto.CardNumber);

        changed |= RecordIfChanged(record, batch, now, nameof(record.LocationCode), record.LocationCode, dto.LocationCode,
            () => record.LocationCode = dto.LocationCode);

        changed |= RecordIfChanged(record, batch, now, nameof(record.ProductName), record.ProductName, dto.ProductName,
            () => record.ProductName = dto.ProductName);

        changed |= RecordIfChanged(record, batch, now, nameof(record.Amount),
            record.Amount.ToString("F2"), dto.Amount.ToString("F2"),
            () => record.Amount = dto.Amount);

        changed |= RecordIfChanged(record, batch, now, nameof(record.TransactionTime),
            AsUtcString(record.TransactionTime), AsUtcString(dto.TransactionTime),
            () => record.TransactionTime = dto.TransactionTime);

        return changed;
    }

    /// <summary>
    /// SQLite returns DateTime values with Kind=Unspecified while the source clients produce
    /// Kind=Utc, and ToString("O") only appends "Z" for the latter. Without normalizing, an
    /// identical stored and incoming value would compare as different and look like a field
    /// change on every re-import.
    /// </summary>
    private static string AsUtcString(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private bool RecordIfChanged(
        TransactionRecord record,
        ImportBatch batch,
        DateTime now,
        string fieldName,
        string? oldValue,
        string? newValue,
        Action applyChange)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return false;
        }

        _db.AuditEntries.Add(new TransactionAuditEntry
        {
            TransactionRecord = record,
            ImportBatchId = batch.Id,
            ChangeType = AuditChangeType.FieldChanged,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAtUtc = now
        });

        applyChange();
        return true;
    }
}

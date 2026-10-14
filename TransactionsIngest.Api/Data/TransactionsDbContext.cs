using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Models;

namespace TransactionsIngest.Api.Data;

public class TransactionsDbContext : DbContext
{
    public TransactionsDbContext(DbContextOptions<TransactionsDbContext> options) : base(options) { }

    public DbSet<TransactionRecord> Transactions => Set<TransactionRecord>();
    public DbSet<TransactionAuditEntry> AuditEntries => Set<TransactionAuditEntry>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The logical unique key from the spec: (SourceSystem, TransactionId).
        // This is what makes the reconciliation idempotent and safe under re-import.
        modelBuilder.Entity<TransactionRecord>()
            .HasIndex(t => new { t.SourceSystem, t.TransactionId })
            .IsUnique();

        modelBuilder.Entity<TransactionRecord>()
            .HasMany(t => t.AuditEntries)
            .WithOne(a => a.TransactionRecord)
            .HasForeignKey(a => a.TransactionRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TransactionRecord>()
            .HasOne(t => t.LastImportBatch)
            .WithMany()
            .HasForeignKey(t => t.LastImportBatchId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TransactionAuditEntry>()
            .HasOne(a => a.ImportBatch)
            .WithMany()
            .HasForeignKey(a => a.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Models;

namespace TransactionsIngest.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionsDbContext _db;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(TransactionsDbContext db, ILogger<TransactionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/transactions - returns all imported transactions for the frontend table.
    /// Optional query params (filtering/sorting enhancements) are supported but not required:
    /// GET /api/transactions?sourceSystem=Online&amp;status=Active
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TransactionListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TransactionListItemDto>>> GetTransactions(
        [FromQuery] string? sourceSystem,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = _db.Transactions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(sourceSystem))
        {
            query = query.Where(t => t.SourceSystem == sourceSystem);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TransactionStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        var results = await query
            .OrderByDescending(t => t.TransactionTime)
            .Select(t => new TransactionListItemDto(
                t.Id,
                t.SourceSystem,
                t.TransactionId,
                t.LocationCode,
                t.ProductName,
                t.Amount,
                t.TransactionTime,
                t.Status.ToString(),
                t.RevisionNumber))
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    /// <summary>GET /api/transactions/{id}/audit - optional enhancement: audit history view.</summary>
    [HttpGet("{id:int}/audit")]
    [ProducesResponseType(typeof(IEnumerable<AuditEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<AuditEntryDto>>> GetAuditHistory(int id, CancellationToken cancellationToken)
    {
        var exists = await _db.Transactions.AnyAsync(t => t.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var entries = await _db.AuditEntries
            .AsNoTracking()
            .Where(a => a.TransactionRecordId == id)
            .OrderBy(a => a.ChangedAtUtc)
            .Select(a => new AuditEntryDto(
                a.Id,
                a.ChangeType.ToString(),
                a.FieldName,
                a.OldValue,
                a.NewValue,
                a.ChangedAtUtc,
                a.ImportBatchId))
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }
}

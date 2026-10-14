namespace TransactionsIngest.Api.Models;

/// <summary>
/// Shape of a transaction as it comes OFF a source system (mocked or real).
/// This is intentionally a plain DTO, separate from TransactionRecord (the persisted entity) -
/// the import process maps one onto the other, which keeps "what the source sent us" decoupled
/// from "how we store it."
/// </summary>
public class SourceTransactionDto
{
    public string SourceSystem { get; set; } = string.Empty;
    public int TransactionId { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionTime { get; set; }
}

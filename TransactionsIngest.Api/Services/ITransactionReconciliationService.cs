using TransactionsIngest.Api.Models;

namespace TransactionsIngest.Api.Services;

public interface ITransactionReconciliationService
{
    Task<ImportResultDto> RunImportAsync(ImportTrigger trigger, CancellationToken cancellationToken = default);
}

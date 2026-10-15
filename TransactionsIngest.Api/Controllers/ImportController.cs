using Microsoft.AspNetCore.Mvc;
using TransactionsIngest.Api.Models;
using TransactionsIngest.Api.Services;

namespace TransactionsIngest.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly ITransactionReconciliationService _reconciliationService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(ITransactionReconciliationService reconciliationService, ILogger<ImportController> logger)
    {
        _reconciliationService = reconciliationService;
        _logger = logger;
    }

    /// <summary>
    /// Manually triggers an import run. The exercise says an external scheduler will call this
    /// same logic hourly in production - this endpoint, and the service method behind it, are
    /// exactly what that scheduled job would invoke too (same code path, just a different caller),
    /// so no changes would be needed to wire up a real scheduler later.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ImportResultDto>> TriggerImport(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _reconciliationService.RunImportAsync(ImportTrigger.Manual, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual import run failed");
            return Problem(
                title: "Import failed",
                detail: "The import run encountered an unexpected error and was rolled back. See server logs for details.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AI.MIS.Api.Controllers;

[ApiController]
[Route("api/copilot")]
public sealed class CopilotController(
    ICopilotPlanningService planningService,
    ISchemaMetadataProvider schemaMetadataProvider,
    ISqlQueryValidator sqlQueryValidator,
    IReadOnlyQueryExecutor queryExecutor,
    IAuditLogger auditLogger,
    IQueryAuthorizationService queryAuthorizationService) : ControllerBase
{
    [HttpPost("interpret")]
    public async Task<IActionResult> Interpret([FromBody] InterpretRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await planningService.InterpretAsync(request.Question, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message }); }
    }

    [HttpPost("query")]
    [EnableRateLimiting("copilot")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Query([FromBody] InterpretRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var interpretation = await planningService.InterpretAsync(request.Question, cancellationToken);
            if (interpretation.NeedsClarification || string.IsNullOrWhiteSpace(interpretation.ProposedSql))
            {
                await auditLogger.LogQueryAsync(new(request.Question, interpretation.ProposedSql, "ClarificationRequired", null, null), cancellationToken);
                return Ok(new { interpretation, result = (CopilotQueryResult?)null });
            }

            var schema = await schemaMetadataProvider.GetApprovedSchemaAsync(cancellationToken);
            sqlQueryValidator.Validate(interpretation.ProposedSql, schema);
            queryAuthorizationService.Validate(interpretation.ProposedSql, User);
            var result = await queryExecutor.ExecuteAsync(interpretation.ProposedSql, cancellationToken);
            await auditLogger.LogQueryAsync(new(request.Question, interpretation.ProposedSql, "Success", result.ExecutionTimeMilliseconds, null), cancellationToken);
            return Ok(new { interpretation, result });
        }
        catch (ArgumentException exception)
        {
            await auditLogger.LogQueryAsync(new(request.Question, null, "InvalidRequest", null, exception.Message), cancellationToken);
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            await auditLogger.LogQueryAsync(new(request.Question, null, "RejectedOrFailed", null, exception.Message), cancellationToken);
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            await auditLogger.LogQueryAsync(new(request.Question, null, "Unauthorized", null, exception.Message), cancellationToken);
            return Forbid();
        }
    }

    public sealed record InterpretRequest(string Question);
}

using System.Security.Claims;
using AI.MIS.Application.ExcelImports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AI.MIS.Api.Controllers;

[ApiController]
[Route("api/excelImports")]
[Authorize]
public sealed class ExcelImportsController(IExcelImportStore imports) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromBody] CreateExcelImportRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var ownerUserId)) return Unauthorized();
        try
        {
            var imported = await imports.CreateAsync(ownerUserId, request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = imported.Id }, imported);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
        }
        catch (SqlException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Excel import storage is unavailable. Verify AiMisErpCopilotDb and apply backend/database/Tables/006_ExcelImports.sql." });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var ownerUserId)) return Unauthorized();
        try
        {
            var report = await imports.GetAsync(ownerUserId, id, cancellationToken);
            return report is null ? NotFound() : Ok(report);
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
        }
        catch (SqlException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Excel import storage is unavailable. Verify AiMisErpCopilotDb and apply backend/database/Tables/006_ExcelImports.sql." });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(subject, out userId);
    }
}

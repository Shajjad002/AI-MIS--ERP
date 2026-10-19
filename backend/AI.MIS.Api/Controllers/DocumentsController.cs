using AI.MIS.Application.Rag;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.MIS.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Administrator,MIS Analyst")]
public sealed class DocumentsController(IDocumentStore documentStore, IRagService ragService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(await documentStore.ListAsync(cancellationToken));

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { error = "A document file is required." });

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            await using var stream = file.OpenReadStream();
            var document = await documentStore.SaveAsync(stream, file.FileName, file.ContentType, file.Length, userId, cancellationToken);
            try
            {
                var chunkCount = await ragService.IndexDocumentAsync(document, cancellationToken);
                return CreatedAtAction(nameof(List), new { id = document.Id }, new { document, indexedChunks = chunkCount });
            }
            catch (InvalidOperationException exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The file was saved, but indexing failed. Retry indexing after correcting the configuration.", detail = exception.Message, documentId = document.Id });
            }
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{documentId:guid}/index")]
    public async Task<IActionResult> Reindex(Guid documentId, CancellationToken cancellationToken)
    {
        var document = (await documentStore.ListAsync(cancellationToken)).SingleOrDefault(item => item.Id == documentId);
        if (document is null) return NotFound();
        try { return Ok(new { documentId, indexedChunks = await ragService.IndexDocumentAsync(document, cancellationToken) }); }
        catch (InvalidOperationException exception) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message }); }
        catch (FileNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] RagSearchRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await ragService.SearchAsync(request, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message }); }
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await ragService.AnswerAsync(request.Question, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message }); }
    }

    public sealed record AskRequest(string Question);
}

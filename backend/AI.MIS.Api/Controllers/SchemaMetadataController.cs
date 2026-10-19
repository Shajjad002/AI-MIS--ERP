using AI.MIS.Application.Copilot;
using Microsoft.AspNetCore.Mvc;

namespace AI.MIS.Api.Controllers;

[ApiController]
[Route("api/schema-metadata")]
public sealed class SchemaMetadataController(ISchemaMetadataProvider schemaMetadataProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await schemaMetadataProvider.GetApprovedSchemaAsync(cancellationToken));
}

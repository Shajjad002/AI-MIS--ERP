using Microsoft.AspNetCore.Mvc;

namespace AI.MIS.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new { application = "AI MIS & ERP Copilot", phase = "Sprint 1 - Foundation", aiEnabled = false, databaseConfigured = false });
}

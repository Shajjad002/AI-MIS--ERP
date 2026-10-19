using Microsoft.AspNetCore.Mvc;
using AI.MIS.Application.Users;

namespace AI.MIS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthenticationService authenticationService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await authenticationService.AuthenticateAsync(request.UserName, request.Password, cancellationToken);
            return result is null ? Unauthorized(new { message = "Invalid username or password." }) : Ok(result);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message }); }
    }

    public sealed record LoginRequest(string UserName, string Password);
}

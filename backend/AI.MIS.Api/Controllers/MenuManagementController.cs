using AI.MIS.Application.Menus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AI.MIS.Api.Controllers;

[ApiController]
[Route("api/menus")]
[Authorize]
public sealed class MenuManagementController(IMenuManagementService menus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListVisible(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        return Ok(await menus.ListVisibleAsync(userId.Value, roles, cancellationToken));
    }

    [HttpGet("manage")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> ListManaged(CancellationToken cancellationToken)
        => Ok(await menus.ListManagedAsync(cancellationToken));

    [HttpGet("roles")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> ListRoles(CancellationToken cancellationToken)
        => Ok(await menus.ListRoleNamesAsync(cancellationToken));

    [HttpPost("roles")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> CreateRole([FromBody] RoleNameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await menus.CreateRoleAsync(request.RoleName, cancellationToken);
            return CreatedAtAction(nameof(ListRoles), null, new { roleName = request.RoleName.Trim() });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpGet("users/{userId:guid}/roles")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> GetUserMenuRoles(Guid userId, CancellationToken cancellationToken)
        => Ok(await menus.ListUserMenuRolesAsync(userId, cancellationToken));

    [HttpPut("users/{userId:guid}/roles")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> SetUserMenuRoles(Guid userId, [FromBody] RoleNamesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await menus.SetUserMenuRolesAsync(userId, request.RoleNames, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create([FromBody] MenuWriteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await menus.CreateMenuAsync(request, cancellationToken);
            return CreatedAtAction(nameof(ListManaged), new { id }, new { id });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MenuWriteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await menus.UpdateMenuAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await menus.DeleteMenuAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    private Guid? GetUserId()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }

    public sealed record RoleNameRequest(string RoleName);
    public sealed record RoleNamesRequest(IReadOnlyList<string> RoleNames);
}

using AI.MIS.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.MIS.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Administrator")]
public sealed class UserManagementController(IUserManagementService users) : ControllerBase
{
    private const int MaxProfileImageBytes = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await users.ListAsync(cancellationToken));

    [HttpPost]
    [RequestSizeLimit(MaxProfileImageBytes + 64 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            byte[]? imageContent = null;
            string? imageContentType = null;
            if (request.ProfileImage is { Length: > 0 } image)
            {
                if (image.Length > MaxProfileImageBytes)
                    return BadRequest(new { message = "Profile image cannot exceed 5 MB." });

                await using var input = image.OpenReadStream();
                using var output = new MemoryStream((int)image.Length);
                await input.CopyToAsync(output, cancellationToken);
                imageContent = output.ToArray();
                imageContentType = DetectImageContentType(imageContent);
                if (imageContentType is null)
                    return BadRequest(new { message = "Profile image must be a valid JPEG, PNG, or WebP image." });
            }

            var command = new CreateUserCommand(
                request.UserName,
                request.DisplayName,
                request.Email,
                request.Password,
                request.Role,
                request.BranchCodes?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [],
                imageContent,
                imageContentType);
            return Ok(await users.CreateAsync(command, cancellationToken));
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpGet("{id:guid}/profile-image")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client, NoStore = false)]
    public async Task<IActionResult> GetProfileImage(Guid id, CancellationToken cancellationToken)
    {
        var image = await users.GetProfileImageAsync(id, cancellationToken);
        return image is null ? NotFound() : File(image.Content, image.ContentType);
    }

    [HttpPut("{id:guid}/access")]
    public async Task<IActionResult> UpdateAccess(Guid id, [FromBody] UpdateUserAccessCommand command, CancellationToken cancellationToken)
    {
        try { await users.UpdateAccessAsync(id, command, cancellationToken); return NoContent(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    public sealed class CreateUserRequest
    {
        public string UserName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string? BranchCodes { get; init; }
        public IFormFile? ProfileImage { get; init; }
    }

    private static string? DetectImageContentType(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 8 &&
            content[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            return "image/jpeg";
        if (content.Length >= 12 &&
            content[..4].SequenceEqual("RIFF"u8) &&
            content[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }
}

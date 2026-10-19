using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AI.MIS.Application.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AI.MIS.Infrastructure.Security;

public sealed class JwtAuthenticationService(IOptions<AuthenticationOptions> options, IUserRepository users) : IAuthenticationService
{
    public async Task<AuthenticationResult?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SigningKey) || settings.SigningKey.Length < 32)
            throw new InvalidOperationException("Authentication is not configured. Set Authentication__SigningKey with at least 32 characters.");

        var user = await users.FindByUserNameAsync(userName, cancellationToken);
        if (user is null || !PasswordHash.Verify(password, user.PasswordHash))
            return null;

        var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(settings.TokenLifetimeMinutes, 5, 1440));
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.Name, user.UserName) };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(user.BranchCodes.Select(branch => new Claim("branch_code", branch)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, expires: expires.UtcDateTime, signingCredentials: credentials);
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, user.UserName, user.Roles);
    }
}

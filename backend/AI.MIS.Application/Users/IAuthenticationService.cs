namespace AI.MIS.Application.Users;

public interface IAuthenticationService
{
    Task<AuthenticationResult?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default);
}

public sealed record AuthenticationResult(string AccessToken, DateTimeOffset ExpiresAtUtc, string UserName, IReadOnlyList<string> Roles);

namespace AI.MIS.Infrastructure.Security;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    public string Issuer { get; init; } = "AI.MIS.Api";
    public string Audience { get; init; } = "AI.MIS.Web";
    public string SigningKey { get; init; } = string.Empty;
    public int TokenLifetimeMinutes { get; init; } = 60;
    public string AdminUserName { get; init; } = string.Empty;
    public string AdminPasswordHash { get; init; } = string.Empty;
}

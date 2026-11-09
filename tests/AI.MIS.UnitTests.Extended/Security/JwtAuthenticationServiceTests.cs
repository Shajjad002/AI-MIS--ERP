using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AI.MIS.Application.Users;
using AI.MIS.Infrastructure.Database;
using AI.MIS.Infrastructure.Security;
using AI.MIS.UnitTests.Extended.Support;
using Microsoft.IdentityModel.Tokens;

namespace AI.MIS.UnitTests.Extended;

public sealed class JwtAuthenticationServiceTests
{
    private const string Key = "unit-test-signing-key-0123456789-abcdefghij";   // 43 chars
    private const string Password = "Correct#Horse1";
    private readonly string passwordHash = PasswordHash.Create(Password);

    private AuthenticatedUser Alice(string[]? roles = null, string[]? branches = null) =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "alice", passwordHash, roles ?? new[] { "Branch User" }, branches ?? new[] { "B01" });

    private static AuthenticationOptions Options(string key = Key, int minutes = 30) =>
        new() { SigningKey = key, Issuer = "test-issuer", Audience = "test-audience", TokenLifetimeMinutes = minutes };

    private JwtAuthenticationService Create(AuthenticatedUser? user, out FakeUserRepository repo, AuthenticationOptions? options = null)
    {
        repo = new FakeUserRepository(name => name == "alice" ? user : null);
        return new JwtAuthenticationService(Microsoft.Extensions.Options.Options.Create(options ?? Options()), repo);
    }

    // ------------------------------------------------------------ configuration guard
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short-key")]
    [InlineData("0123456789012345678901234567890")]       // 31 characters: one short
    public async Task AuthenticateAsync_RefusesToRun_WithoutAStrongSigningKey(string key)
    {
        var service = Create(Alice(), out var repo, Options(key));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuthenticateAsync("alice", Password));

        Assert.Contains("Authentication__SigningKey", ex.Message);
        Assert.Empty(repo.Lookups);                       // fails before touching the user store
    }

    // ------------------------------------------------------------ credentials
    [Fact]
    public async Task AuthenticateAsync_ReturnsNull_ForUnknownUser()
    {
        var service = Create(Alice(), out _);

        Assert.Null(await service.AuthenticateAsync("nobody", Password));
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsNull_ForWrongPassword()
    {
        var service = Create(Alice(), out _);

        Assert.Null(await service.AuthenticateAsync("alice", "wrong"));
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsNull_WhenStoredHashIsCorrupt()
    {
        var corrupt = Alice() with { PasswordHash = "garbage" };
        var service = Create(corrupt, out _);

        Assert.Null(await service.AuthenticateAsync("alice", Password));
    }

    // ------------------------------------------------------------ token contents
    [Fact]
    public async Task AuthenticateAsync_IssuesSignedTokenWithIdentityRolesAndBranches()
    {
        var service = Create(Alice(["Branch User", "Viewer"], ["B01", "B03"]), out _);

        var result = await service.AuthenticateAsync("alice", Password);

        Assert.NotNull(result);
        Assert.Equal("alice", result!.UserName);
        Assert.Equal(new[] { "Branch User", "Viewer" }, result.Roles);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Equal("test-issuer", token.Issuer);
        Assert.Contains("test-audience", token.Audiences);
        Assert.Equal("11111111-1111-1111-1111-111111111111", token.Subject);
        Assert.Equal(new[] { "B01", "B03" }, token.Claims.Where(c => c.Type == "branch_code").Select(c => c.Value).OrderBy(v => v));
        var roles = token.Claims.Where(c => c.Type is "role" or ClaimTypes.Role).Select(c => c.Value).OrderBy(v => v);
        Assert.Equal(new[] { "Branch User", "Viewer" }, roles);
    }

    [Fact]
    public async Task AuthenticateAsync_DoesNotPutThePasswordOrHashInTheToken()
    {
        var service = Create(Alice(), out _);

        var result = await service.AuthenticateAsync("alice", Password);

        var payload = new JwtSecurityTokenHandler().ReadJwtToken(result!.AccessToken).Payload.SerializeToJson();
        Assert.DoesNotContain(Password, payload);
        Assert.DoesNotContain(passwordHash, payload);
    }

    [Fact]
    public async Task AuthenticateAsync_TokenIsRejectedWhenValidatedWithADifferentKey()
    {
        var service = Create(Alice(), out _);
        var result = await service.AuthenticateAsync("alice", Password);

        var wrongKey = Validation("another-signing-key-0123456789-abcdefghijk");

        Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(result!.AccessToken, wrongKey, out _));
    }

    // ------------------------------------------------------------ lifetime clamp
    [Theory]
    [InlineData(0, 5)]            // below minimum -> 5 minutes
    [InlineData(-10, 5)]
    [InlineData(30, 30)]
    [InlineData(100_000, 1440)]   // above maximum -> 24 hours
    public async Task AuthenticateAsync_ClampsTokenLifetime(int configuredMinutes, int expectedMinutes)
    {
        var service = Create(Alice(), out _, Options(minutes: configuredMinutes));

        var result = await service.AuthenticateAsync("alice", Password);

        var remaining = result!.ExpiresAtUtc - DateTimeOffset.UtcNow;
        Assert.InRange(remaining.TotalMinutes, expectedMinutes - 1, expectedMinutes + 0.5);
    }

    // ------------------------------------------------------------ end-to-end with the real authorizer
    [Fact]
    public async Task IssuedToken_RoundTripsIntoQueryAuthorization_ForABranchUser()
    {
        var service = Create(Alice(["Branch User"], ["B01"]), out _);
        var result = await service.AuthenticateAsync("alice", Password);
        var principal = new JwtSecurityTokenHandler().ValidateToken(result!.AccessToken, Validation(Key), out _);
        var authorization = new QueryAuthorizationService();

        Assert.Null(Record.Exception(() =>
            authorization.Validate("SELECT * FROM LoanCollection WHERE BranchCode = 'B01'", principal)));
        Assert.Throws<UnauthorizedAccessException>(() =>
            authorization.Validate("SELECT * FROM LoanCollection WHERE BranchCode = 'B02'", principal));
        Assert.Throws<UnauthorizedAccessException>(() =>
            authorization.Validate("SELECT * FROM LoanCollection", principal));
    }

    [Fact]
    public async Task IssuedToken_RoundTripsIntoQueryAuthorization_ForAnAdministrator()
    {
        var service = Create(Alice(["Administrator"], []), out _);
        var result = await service.AuthenticateAsync("alice", Password);
        var principal = new JwtSecurityTokenHandler().ValidateToken(result!.AccessToken, Validation(Key), out _);

        Assert.Null(Record.Exception(() => new QueryAuthorizationService().Validate("SELECT * FROM LoanCollection", principal)));
    }

    private static TokenValidationParameters Validation(string key) => new()
    {
        ValidIssuer = "test-issuer",
        ValidAudience = "test-audience",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = true
    };
}

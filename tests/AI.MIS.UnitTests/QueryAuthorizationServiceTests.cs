using System.Security.Claims;
using AI.MIS.Infrastructure.Database;

namespace AI.MIS.UnitTests;

public sealed class QueryAuthorizationServiceTests
{
    private readonly QueryAuthorizationService service = new();

    [Theory]
    [InlineData("Administrator")]
    [InlineData("MIS Analyst")]
    public void Validate_AllowsPrivilegedRoles(string role)
    {
        service.Validate("SELECT * FROM Sales", User(role));
    }

    [Fact]
    public void Validate_AllowsBranchUserWithAuthorizedBranchFilter()
    {
        service.Validate("SELECT * FROM Sales WHERE BranchCode = 'B01'", BranchUser("B01"));
    }

    [Theory]
    [InlineData("SELECT * FROM Sales")]
    [InlineData("SELECT * FROM Sales WHERE BranchCode = 'B02'")]
    [InlineData("SELECT * FROM Sales WHERE BranchCode = 'B01' OR Amount > 0")]
    [InlineData("SELECT * FROM Sales WHERE BranchCode = 'B01'; SELECT * FROM Sales WHERE BranchCode = 'B01'")]
    public void Validate_RejectsBranchQueriesWithoutGuaranteedAuthorizedScope(string sql)
    {
        Assert.Throws<UnauthorizedAccessException>(() => service.Validate(sql, BranchUser("B01")));
    }

    [Fact]
    public void Validate_RejectsUserWithoutQueryRole()
    {
        Assert.Throws<UnauthorizedAccessException>(() => service.Validate("SELECT 1", User("Viewer")));
    }

    [Fact]
    public void Validate_RejectsBranchUserWithoutBranchClaims()
    {
        Assert.Throws<UnauthorizedAccessException>(() => service.Validate("SELECT 1", User("Branch User")));
    }

    private static ClaimsPrincipal User(string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "unit-test"));

    private static ClaimsPrincipal BranchUser(string branch) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.Role, "Branch User"), new Claim("branch_code", branch)
    ], "unit-test"));
}

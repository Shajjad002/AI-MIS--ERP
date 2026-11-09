using System.Security.Claims;
using AI.MIS.Application.Copilot.Models;
using AI.MIS.Infrastructure.AI;

namespace AI.MIS.UnitTests.Extended.Support;

public static class TestUsers
{
    public const string BranchUserRole = "Branch User";   // exact strings used by QueryAuthorizationService
    public const string AdminRole = "Administrator";
    public const string AnalystRole = "MIS Analyst";

    public static ClaimsPrincipal With(string? role, params string[] branches)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        claims.AddRange(branches.Select(b => new Claim("branch_code", b)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "unit-test"));
    }

    public static ClaimsPrincipal Admin() => With(AdminRole);
    public static ClaimsPrincipal Analyst() => With(AnalystRole);
    public static ClaimsPrincipal Branch(params string[] branches) => With(BranchUserRole, branches);
}

public static class Schema
{
    /// <summary>The real approved schema used by the application.</summary>
    public static IReadOnlyList<SchemaTable> Approved() =>
        new InMemorySchemaMetadataProvider().GetApprovedSchemaAsync().GetAwaiter().GetResult();
}

public static class Reflect
{
    /// <summary>Reads a property from an anonymous result object such as new { interpretation, result }.</summary>
    public static T? Prop<T>(object? target, string name)
    {
        var p = target!.GetType().GetProperty(name) ?? throw new InvalidOperationException($"No property '{name}' on {target.GetType()}.");
        return (T?)p.GetValue(target);
    }
}

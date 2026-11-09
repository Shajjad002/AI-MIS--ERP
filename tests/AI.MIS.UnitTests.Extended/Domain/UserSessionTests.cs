using System.Security.Claims;
using AI.MIS.Domain.Entities;
using AI.MIS.UnitTests.Extended.Support;

namespace AI.MIS.UnitTests.Extended;

public sealed class UserSessionTests
{
    /// <summary>The claims UserSession.Init requires (it dereferences .Value on them without a null check).</summary>
    private static List<Claim> RequiredClaims() =>
    [
        new("Username", "u1"), new("UserType", "Staff"), new("EIN", "1001"), new(ClaimTypes.Name, "Rahim Uddin"),
        new("WorkStationNo", "0212"), new("BaseWorkStationNo", "0212"), new("WorkStationType", "Branch"),
        new("IsHoliday", "false"), new("IsFirstTimeLogin", "true"), new("ProgramCode", ProgramList.Microcredit)
    ];

    private static UserSession Init(IEnumerable<Claim> claims, DateTime? systemDay = null, string dayStatus = "", string isProduction = "0")
    {
        var session = new UserSession();
        session.Init(claims, systemDay, dayStatus, isProduction);
        return session;
    }

    // ================================================================ Init
    [Fact]
    public void Init_MapsRequiredClaims()
    {
        var session = Init(RequiredClaims());

        Assert.Equal("u1", session.Username);
        Assert.Equal("Staff", session.UserType);
        Assert.Equal("1001", session.EIN);
        Assert.Equal("Rahim Uddin", session.Name);
        Assert.Equal("0212", session.WorkStationNo);
        Assert.Equal("Branch", session.WorkStationType);
        Assert.Equal("001", session.ProgramCode);
        Assert.False(session.IsHoliday);
        Assert.True(session.IsFirstTimeLogin);
    }

    [Fact]
    public void Init_MapsOptionalClaims_AndRoles()
    {
        var claims = RequiredClaims();
        claims.AddRange([
            new Claim("BranchCode", "0212"), new Claim("BranchName", "Agrabad"), new Claim(ClaimTypes.Email, "r@example.org"),
            new Claim(ClaimTypes.Role, "Administrator"), new Claim(ClaimTypes.Role, "MIS Analyst"),
            new Claim("IsFaceRecognitionEnabled", "true")
        ]);

        var session = Init(claims);

        Assert.Equal("0212", session.BranchCode);
        Assert.Equal("Agrabad", session.BranchName);
        Assert.Equal("r@example.org", session.Email);
        Assert.Equal(new[] { "Administrator", "MIS Analyst" }, session.Roles);
        Assert.True(session.IsFaceRecognitionEnabled);
    }

    [Fact]
    public void Init_LeavesOptionalValuesNull_WhenClaimsAreAbsent()
    {
        var session = Init(RequiredClaims());

        Assert.Null(session.Email);
        Assert.Null(session.PhotoURL);
        Assert.Null(session.BranchName);
        Assert.Null(session.RegionCode);
        Assert.Empty(session.Roles);
        Assert.False(session.IsFaceRecognitionEnabled);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData("true", false)]           // only the exact string "1" means production
    public void Init_TreatsOnlyTheExactStringOneAsProduction(string flag, bool expected)
    {
        Assert.Equal(expected, Init(RequiredClaims(), isProduction: flag).IsProduction);
    }

    [Fact]
    public void Init_TakesSystemDayAndStatusFromTheClaims_WhenNoOverridesAreGiven()
    {
        var claims = RequiredClaims();
        claims.Add(new Claim("WorkStationSystemDay", "2026-08-15"));
        claims.Add(new Claim("WorkStationDayStatus", "Open"));

        var session = Init(claims);

        Assert.Equal(new DateTime(2026, 8, 15), session.WorkStationSystemDay.Date);
        Assert.Equal("Open", session.WorkStationDayStatus);
    }

    [Fact]
    public void Init_PrefersExplicitSystemDayAndStatus_OverTheClaims()
    {
        var claims = RequiredClaims();
        claims.Add(new Claim("WorkStationSystemDay", "2026-08-15"));
        claims.Add(new Claim("WorkStationDayStatus", "Open"));

        var session = Init(claims, new DateTime(2026, 9, 1), "Closed");

        Assert.Equal(new DateTime(2026, 9, 1), session.WorkStationSystemDay);
        Assert.Equal("Closed", session.WorkStationDayStatus);
    }

    [Fact]
    public void Init_IgnoresABlankSystemDayClaim()
    {
        var claims = RequiredClaims();
        claims.Add(new Claim("WorkStationSystemDay", "  "));

        Assert.Equal(default, Init(claims).WorkStationSystemDay);
    }

    // ================================================================ permissions
    [Fact]
    public void CheckPermission_Succeeds_WhenTheOperationIsGranted_AndExposesTheProgramAdminGrant()
    {
        var session = new UserSession();
        session.OperationPermissionList.Add(new OperationPermission { ScreenOperationCode = "LOAN.APPROVE", RoleCode = "MicrofinanceProgramAdmin" });

        var result = session.CheckPermission("LOAN.APPROVE");

        Assert.True(result.IsSuccessful);
        Assert.Equal("Authorized", result.Message);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public void CheckPermission_SucceedsWithoutAdminData_ForOrdinaryRoles()
    {
        var session = new UserSession();
        session.OperationPermissionList.Add(new OperationPermission { ScreenOperationCode = "LOAN.VIEW", RoleCode = "BranchManager" });

        var result = session.CheckPermission("LOAN.VIEW");

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Data);
    }

    [Fact]
    public void CheckPermission_Fails_WhenTheOperationIsNotGranted()
    {
        var result = new UserSession().CheckPermission("LOAN.DELETE");

        Assert.False(result.IsSuccessful);
        Assert.Equal("You are not authorized to do this operation.", result.Message);
    }

    [Fact]
    public void HavePermission_ReflectsWhetherTheOperationIsGranted()
    {
        var session = new UserSession();
        session.OperationPermissionList.Add(new OperationPermission { ScreenOperationCode = "A", RoleCode = "r" });

        Assert.True(session.HavePermission("A").IsSuccessful);
        Assert.False(session.HavePermission("B").IsSuccessful);
    }

    [Fact]
    [Trait("Category", TestTraits.SuspectedDefect)]
    public void HavePermission_WhenGranted_ShouldNotSayTheUserIsNotAuthorized()
    {
        // SUSPECTED DEFECT: the success branch reuses the failure text "You are not authorized for this operation."
        var session = new UserSession();
        session.OperationPermissionList.Add(new OperationPermission { ScreenOperationCode = "A", RoleCode = "r" });

        var result = session.HavePermission("A");

        Assert.True(result.IsSuccessful);
        Assert.DoesNotContain("not authorized", result.Message);
    }
}

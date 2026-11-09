using AI.MIS.Infrastructure.Database;
using AI.MIS.UnitTests.Extended.Support;

namespace AI.MIS.UnitTests.Extended;

/// <summary>Complements tests/AI.MIS.UnitTests/QueryAuthorizationServiceTests.cs - focuses on bypass attempts.</summary>
public sealed class QueryAuthorizationServiceExtendedTests
{
    private readonly QueryAuthorizationService service = new();

    // ------------------------------------------------------------ roles
    [Theory]
    [InlineData("Administrator")]
    [InlineData("MIS Analyst")]
    public void PrivilegedRoles_SkipBranchChecksEntirely(string role)
    {
        // Even text that is not SQL passes: privileged users are authorized before the query is parsed.
        var ex = Record.Exception(() => service.Validate("not even sql", TestUsers.With(role)));

        Assert.Null(ex);
    }

    [Fact]
    public void UserWithNoRoles_IsRejected()
    {
        Assert.Throws<UnauthorizedAccessException>(() => service.Validate("SELECT 1", TestUsers.With(null)));
    }

    [Fact]
    public void UnauthenticatedPrincipal_IsRejected()
    {
        Assert.Throws<UnauthorizedAccessException>(() => service.Validate("SELECT 1", new System.Security.Claims.ClaimsPrincipal()));
    }

    [Theory]
    [InlineData("BranchUser")]      // the real role is "Branch User" (with a space)
    [InlineData("MISAnalyst")]
    [InlineData("Admin")]
    [InlineData("Viewer")]
    public void UnknownOrMisspelledRoles_AreRejected(string role)
    {
        Assert.Throws<UnauthorizedAccessException>(() => service.Validate("SELECT 1", TestUsers.With(role, "B01")));
    }

    // ------------------------------------------------------------ allowed branch queries
    [Theory]
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = 'B01'")]
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = 'b01'")]                       // case-insensitive
    [InlineData("SELECT * FROM LoanCollection lc WHERE lc.BranchCode = 'B01'")]
    [InlineData("SELECT * FROM LoanCollection AS lc WHERE lc.BranchCode = 'B01'")]
    [InlineData("SELECT * FROM LoanCollection WHERE 'B01' = BranchCode")]                       // literal on the left
    [InlineData("SELECT * FROM LoanCollection WHERE (BranchCode = 'B01')")]
    [InlineData("SELECT * FROM LoanCollection WHERE (BranchCode = 'B01') AND CollectionAmount > 0")]
    [InlineData("SELECT * FROM LoanCollection WHERE CollectionAmount > 0 AND BranchCode = 'B01'")]
    [InlineData("SELECT BranchCode, SUM(CollectionAmount) AS T FROM LoanCollection WHERE BranchCode = 'B01' GROUP BY BranchCode")]
    public void BranchUser_IsAllowed_WhenQueryIsGuaranteedToStayInsideTheirBranch(string sql)
    {
        var ex = Record.Exception(() => service.Validate(sql, TestUsers.Branch("B01")));

        Assert.Null(ex);
    }

    // ------------------------------------------------------------ bypass attempts
    [Theory]
    [InlineData("SELECT * FROM LoanCollection")]                                                // no filter
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = 'B02'")]                       // someone else's branch
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = 'B01' OR 1 = 1")]              // classic tautology
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = 'B01' OR BranchCode = 'B02'")]  // OR with a foreign branch
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode <> 'B02'")]                      // inequality is not a guarantee
    [InlineData("SELECT * FROM LoanCollection WHERE NOT BranchCode = 'B02'")]
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode IN ('B01')")]                    // IN is conservatively refused
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode LIKE 'B%'")]
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = BranchCode")]                  // column = column
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = 1")]                           // not a string literal
    [InlineData("SELECT * FROM LoanCollection WHERE AccountNo = 'B01'")]                        // right literal, wrong column
    [InlineData("SELECT * FROM LoanCollection lc WHERE x.BranchCode = 'B01'")]                  // wrong qualifier
    public void BranchUser_IsRejected_WhenFilterDoesNotGuaranteeTheirBranch(string sql)
    {
        Assert.Throws<UnauthorizedAccessException>(() => service.Validate(sql, TestUsers.Branch("B01")));
    }

    [Theory]
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = 'B01' UNION ALL SELECT * FROM LoanCollection")]
    [InlineData("SELECT * FROM (SELECT * FROM LoanCollection WHERE BranchCode = 'B01') x")]
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = 'B01' AND AccountNo IN (SELECT AccountNo FROM LoanRecoverable)")]
    [InlineData("SELECT * FROM LoanCollection c JOIN LoanRecoverable r ON r.AccountNo = c.AccountNo WHERE c.BranchCode = 'B01'")]
    [InlineData("SELECT * FROM LoanCollection WHERE BranchCode = 'B01'; SELECT * FROM LoanCollection")]
    public void BranchUser_IsRejected_ForUnionsSubqueriesJoinsAndBatches(string sql)
    {
        Assert.Throws<UnauthorizedAccessException>(() => service.Validate(sql, TestUsers.Branch("B01")));
    }

    [Theory]
    [InlineData("SELEC * FROM")]
    [InlineData("")]
    public void BranchUser_IsRejected_WhenQueryCannotBeParsed(string sql)
    {
        Assert.Throws<UnauthorizedAccessException>(() => service.Validate(sql, TestUsers.Branch("B01")));
    }

    // ------------------------------------------------------------ several branches
    [Fact]
    public void BranchUserWithTwoBranches_MayFilterOnEither_ButNotOnAThird()
    {
        var user = TestUsers.Branch("B01", "B03");

        Assert.Null(Record.Exception(() => service.Validate("SELECT * FROM LoanCollection WHERE BranchCode = 'B03'", user)));
        Assert.Null(Record.Exception(() => service.Validate("SELECT * FROM LoanCollection WHERE BranchCode = 'B01' OR BranchCode = 'B03'", user)));
        Assert.Throws<UnauthorizedAccessException>(() =>
            service.Validate("SELECT * FROM LoanCollection WHERE BranchCode = 'B01' OR BranchCode = 'B02'", user));
    }

    [Fact]
    public void BranchUserWithoutBranchClaims_IsRejected_EvenForAFilteredQuery()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            service.Validate("SELECT * FROM LoanCollection WHERE BranchCode = 'B01'", TestUsers.Branch()));
    }
}

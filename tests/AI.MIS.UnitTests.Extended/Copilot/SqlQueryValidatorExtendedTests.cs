using AI.MIS.Application.Copilot.Models;
using AI.MIS.Infrastructure.Database;
using AI.MIS.UnitTests.Extended.Support;

namespace AI.MIS.UnitTests.Extended;

/// <summary>Complements tests/AI.MIS.UnitTests/SqlQueryValidatorTests.cs using the real approved schema.</summary>
public sealed class SqlQueryValidatorExtendedTests
{
    private readonly SqlQueryValidator validator = new();
    private readonly IReadOnlyList<SchemaTable> schema = Schema.Approved();

    // ------------------------------------------------------------ accepted queries
    [Theory]
    [InlineData("SELECT * FROM Branch")]
    [InlineData("select branchcode from BRANCH")]                                   // case-insensitive
    [InlineData("SELECT BranchCode FROM dbo.Branch")]                                // schema-qualified
    [InlineData("SELECT TOP (5) BranchCode, RecoverableAmount FROM LoanRecoverable ORDER BY RecoverableAmount DESC")]
    [InlineData("SELECT BranchCode, SUM(CollectionAmount) AS Total FROM LoanCollection WHERE CollectionDate >= '2026-08-01' AND CollectionDate < '2026-09-01' GROUP BY BranchCode")]
    [InlineData("SELECT b.BranchName, SUM(c.CollectionAmount) AS Total FROM Branch b JOIN LoanCollection c ON c.BranchCode = b.BranchCode GROUP BY b.BranchName")]
    [InlineData("SELECT BranchCode FROM Branch WHERE BranchCode IN (SELECT BranchCode FROM LoanCollection)")]
    public void Validate_AcceptsReadOnlyQueriesOverApprovedSchema(string sql)
    {
        var ex = Record.Exception(() => validator.Validate(sql, schema));

        Assert.Null(ex);
    }

    // ------------------------------------------------------------ rejected queries
    [Theory]
    [InlineData("INSERT INTO Branch (BranchCode) VALUES ('x')")]
    [InlineData("UPDATE Branch SET BranchName = 'x'")]
    [InlineData("TRUNCATE TABLE LoanCollection")]
    [InlineData("DROP TABLE Branch")]
    [InlineData("EXEC sp_who")]
    [InlineData("CREATE TABLE x (a int)")]
    [InlineData("SELECT BranchCode FROM Branch; DELETE FROM Branch")]
    public void Validate_RejectsWritesAndMultipleStatements(string sql)
    {
        Assert.Throws<InvalidOperationException>(() => validator.Validate(sql, schema));
    }

    [Theory]
    [InlineData("SELEC * FROM Branch")]
    [InlineData("SELECT * FROM")]
    [InlineData("SELECT ((( FROM Branch")]
    public void Validate_RejectsSyntaxErrors_WithParseMessage(string sql)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate(sql, schema));

        Assert.Contains("could not be parsed", ex.Message);
    }

    [Theory]
    [InlineData("SELECT name FROM sys.objects")]
    [InlineData("SELECT * FROM sys.sql_logins")]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.TABLES")]
    [InlineData("SELECT BranchCode FROM Branch b JOIN Payroll p ON p.BranchCode = b.BranchCode")]
    [InlineData("SELECT BranchCode FROM Branch WHERE BranchCode IN (SELECT BranchCode FROM Payroll)")]
    public void Validate_RejectsUnapprovedTablesAnywhereInTheQuery(string sql)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate(sql, schema));

        Assert.Contains("not approved", ex.Message);
    }

    [Theory]
    [InlineData("SELECT Salary FROM Branch")]
    [InlineData("SELECT BranchCode FROM Branch WHERE Salary > 0")]
    [InlineData("SELECT b.Salary FROM Branch b")]
    [InlineData("SELECT RecoverableAmount FROM LoanCollection")]                    // column exists, but on another table
    public void Validate_RejectsUnapprovedColumns(string sql)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate(sql, schema));

        Assert.Contains("column", ex.Message);
    }

    [Fact]
    public void Validate_RejectsUnknownTableAlias()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate("SELECT x.BranchCode FROM Branch b", schema));

        Assert.Contains("alias", ex.Message);
    }

    [Fact]
    public void Validate_ChecksColumnsAgainstTheAliasedTable()
    {
        // BranchName belongs to Branch, not LoanCollection, so c.BranchName must be refused.
        Assert.Throws<InvalidOperationException>(() =>
            validator.Validate("SELECT c.BranchName FROM Branch b JOIN LoanCollection c ON c.BranchCode = b.BranchCode", schema));
    }

    // ------------------------------------------------------------ suspected defects (desired behaviour)
    [Fact]
    [Trait("Category", TestTraits.SuspectedDefect)]
    public void Validate_ShouldAllowCountStar()
    {
        // SUSPECTED DEFECT: COUNT(*) is parsed as a ColumnReferenceExpression of type Wildcard whose
        // MultiPartIdentifier is null, so ReferenceVisitor.Visit(ColumnReferenceExpression) would throw
        // NullReferenceException (-> HTTP 500) for every "how many ...?" question.
        var ex = Record.Exception(() => validator.Validate("SELECT COUNT(*) AS Total FROM Branch", schema));

        Assert.Null(ex);
    }

    [Fact]
    [Trait("Category", TestTraits.SuspectedDefect)]
    public void Validate_ShouldAllowOrderByColumnAlias()
    {
        // SUSPECTED DEFECT: "ORDER BY Total" is a column reference to a SELECT alias, which is not in the schema,
        // so a very common LLM-generated pattern is rejected as "column that is not approved".
        var sql = "SELECT BranchCode, SUM(CollectionAmount) AS Total FROM LoanCollection GROUP BY BranchCode ORDER BY Total DESC";

        var ex = Record.Exception(() => validator.Validate(sql, schema));

        Assert.Null(ex);
    }

    [Fact]
    [Trait("Category", TestTraits.SuspectedDefect)]
    public void Validate_ShouldRejectSelectInto()
    {
        // SUSPECTED DEFECT: SELECT ... INTO is a SelectStatement and the INTO target is not a NamedTableReference,
        // so it is never checked. It creates a table (a write). The read-only DB login is the only protection.
        Assert.Throws<InvalidOperationException>(() =>
            validator.Validate("SELECT BranchCode INTO dbo.Leak FROM Branch", schema));
    }

    [Theory]
    [InlineData("SELECT BranchCode FROM OtherDb.dbo.Branch")]
    [InlineData("SELECT BranchCode FROM LinkedServer.ERP.dbo.Branch")]
    [Trait("Category", TestTraits.SuspectedDefect)]
    public void Validate_ShouldRejectCrossDatabaseAndLinkedServerReferences(string sql)
    {
        // SUSPECTED DEFECT: only SchemaObject.BaseIdentifier ("Branch") is compared with the whitelist,
        // so a different database / linked server with an identically named table passes validation.
        Assert.Throws<InvalidOperationException>(() => validator.Validate(sql, schema));
    }
}

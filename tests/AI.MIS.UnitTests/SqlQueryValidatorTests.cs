using AI.MIS.Application.Copilot.Models;
using AI.MIS.Infrastructure.Database;

namespace AI.MIS.UnitTests;

public sealed class SqlQueryValidatorTests
{
    private readonly SqlQueryValidator validator = new();
    private readonly IReadOnlyList<SchemaTable> schema =
    [
        new SchemaTable("Sales", "Sales data", [
            new SchemaColumn("BranchCode", "Branch", "varchar"),
            new SchemaColumn("Amount", "Amount", "decimal")])
    ];

    [Theory]
    [InlineData("SELECT Amount FROM Sales")]
    [InlineData("SELECT s.Amount FROM Sales AS s WHERE s.BranchCode = 'B01'")]
    public void Validate_AllowsSingleSelectUsingApprovedSchema(string sql)
    {
        validator.Validate(sql, schema);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsEmptyQuery(string sql)
    {
        Assert.Throws<ArgumentException>(() => validator.Validate(sql, schema));
    }

    [Theory]
    [InlineData("DELETE FROM Sales")]
    [InlineData("SELECT Amount FROM Sales; SELECT Amount FROM Sales")]
    [InlineData("SELECT Amount FROM Sales; DROP TABLE Sales")]
    public void Validate_RejectsNonSelectOrMultipleStatements(string sql)
    {
        Assert.Throws<InvalidOperationException>(() => validator.Validate(sql, schema));
    }

    [Fact]
    public void Validate_RejectsUnapprovedTable()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate("SELECT Amount FROM Payroll", schema));
        Assert.Contains("not approved", exception.Message);
    }

    [Fact]
    public void Validate_RejectsUnapprovedColumn()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate("SELECT Salary FROM Sales", schema));
        Assert.Contains("column", exception.Message);
    }
}

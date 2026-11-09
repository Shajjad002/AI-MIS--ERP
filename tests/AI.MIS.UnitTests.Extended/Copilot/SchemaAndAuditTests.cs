using AI.MIS.Application.Copilot;
using AI.MIS.Infrastructure.AI;
using AI.MIS.Infrastructure.Audit;
using AI.MIS.UnitTests.Extended.Support;
using Microsoft.Extensions.Logging;

namespace AI.MIS.UnitTests.Extended;

public sealed class InMemorySchemaMetadataProviderTests
{
    private readonly IReadOnlyList<AI.MIS.Application.Copilot.Models.SchemaTable> schema = Schema.Approved();

    [Fact]
    public void Schema_ContainsTheDocumentedTables()
    {
        var names = schema.Select(t => t.Name).ToArray();

        Assert.Contains("LoanRecoverable", names);
        Assert.Contains("LoanCollection", names);
        Assert.Contains("Branch", names);
    }

    [Fact]
    public void EveryTable_HasABranchCodeColumn_BecauseBranchAuthorizationDependsOnIt()
    {
        // QueryAuthorizationService only recognises a column literally named "BranchCode".
        // A table added to the schema without it could never be queried by a Branch User.
        Assert.All(schema, table =>
            Assert.Contains(table.Columns, c => string.Equals(c.Name, "BranchCode", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void TableAndColumnNames_AreUniqueAndDescribed()
    {
        Assert.Equal(schema.Count, schema.Select(t => t.Name.ToLowerInvariant()).Distinct().Count());
        Assert.All(schema, table =>
        {
            Assert.False(string.IsNullOrWhiteSpace(table.Description));
            Assert.NotEmpty(table.Columns);
            Assert.Equal(table.Columns.Count, table.Columns.Select(c => c.Name.ToLowerInvariant()).Distinct().Count());
            Assert.All(table.Columns, c =>
            {
                Assert.False(string.IsNullOrWhiteSpace(c.Description));
                Assert.False(string.IsNullOrWhiteSpace(c.DataType));
            });
        });
    }

    [Fact]
    public async Task Provider_ReturnsTheSameSchemaOnEveryCall()
    {
        var provider = new InMemorySchemaMetadataProvider();

        Assert.Same(await provider.GetApprovedSchemaAsync(), await provider.GetApprovedSchemaAsync());
    }
}

public sealed class LoggingAuditLoggerTests
{
    [Fact]
    public async Task LogQueryAsync_WritesOneInformationEntryWithAllFields()
    {
        var logger = new ListLogger<LoggingAuditLogger>();
        var audit = new LoggingAuditLogger(logger);

        await audit.LogQueryAsync(new QueryAuditEntry("Show August collection", "SELECT 1", "Success", 12, null));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("Outcome=Success", entry.Message);
        Assert.Contains("Question=Show August collection", entry.Message);
        Assert.Contains("Sql=SELECT 1", entry.Message);
        Assert.Contains("ExecutionTimeMs=12", entry.Message);
    }

    [Fact]
    public async Task LogQueryAsync_AcceptsMissingSqlAndTimingForRejectedQueries()
    {
        var logger = new ListLogger<LoggingAuditLogger>();

        await new LoggingAuditLogger(logger).LogQueryAsync(new QueryAuditEntry("q", null, "Unauthorized", null, "no access"));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("Outcome=Unauthorized", entry.Message);
        Assert.Contains("Error=no access", entry.Message);
    }
}

using AI.MIS.Infrastructure.Database;

namespace AI.MIS.UnitTests.Extended;

/// <summary>Only the configuration guard is unit-testable; running SQL needs a database (integration test).</summary>
public sealed class SqlReadOnlyQueryExecutorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SET_IN_USER_SECRETS_OR_ENVIRONMENT")]       // the placeholder shipped in appsettings.json
    public async Task ExecuteAsync_RefusesToRun_UntilTheReadOnlyConnectionIsConfigured(string connectionString)
    {
        var executor = new SqlReadOnlyQueryExecutor(
            Microsoft.Extensions.Options.Options.Create(new DatabaseOptions { ErpReadOnlyDatabase = connectionString }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync("SELECT 1"));

        Assert.Contains("ErpReadOnlyDatabase", ex.Message);
    }
}

using System.Diagnostics;
using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AI.MIS.Infrastructure.Database;

public sealed class SqlReadOnlyQueryExecutor(IOptions<DatabaseOptions> options) : IReadOnlyQueryExecutor
{
    public async Task<CopilotQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        var connectionString = options.Value.ErpReadOnlyDatabase;
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString == "SET_IN_USER_SECRETS_OR_ENVIRONMENT")
            throw new InvalidOperationException("The ERP read-only database is not configured. Set ConnectionStrings__ErpReadOnlyDatabase using user secrets or an environment variable.");

        var timeout = Math.Clamp(options.Value.CommandTimeoutSeconds, 1, 120);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = timeout
        };
        var stopwatch = Stopwatch.StartNew();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
                row[columns[index]] = await reader.IsDBNullAsync(index, cancellationToken) ? null : reader.GetValue(index);
            rows.Add(row);
        }

        stopwatch.Stop();
        var numericColumn = columns.FirstOrDefault(column =>
            rows.Any(row => row[column] is byte or short or int or long or float or double or decimal));
        var summary = numericColumn is null
            ? $"The query returned {rows.Count:N0} row{(rows.Count == 1 ? string.Empty : "s")}."
            : $"The query returned {rows.Count:N0} row{(rows.Count == 1 ? string.Empty : "s")}. {numericColumn} totals {rows.Sum(row => Convert.ToDecimal(row[numericColumn] ?? 0)):N2}.";

        return new CopilotQueryResult(columns, rows, stopwatch.ElapsedMilliseconds, summary);
    }
}

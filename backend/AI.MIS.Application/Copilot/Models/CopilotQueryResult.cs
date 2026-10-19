namespace AI.MIS.Application.Copilot.Models;

public sealed record CopilotQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    long ExecutionTimeMilliseconds,
    string Summary);

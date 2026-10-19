namespace AI.MIS.Application.Copilot;

public interface IAuditLogger
{
    Task LogQueryAsync(QueryAuditEntry entry, CancellationToken cancellationToken = default);
}

public sealed record QueryAuditEntry(
    string Question,
    string? Sql,
    string Outcome,
    long? ExecutionTimeMilliseconds,
    string? Error);

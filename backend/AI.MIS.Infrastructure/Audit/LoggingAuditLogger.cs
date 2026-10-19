using AI.MIS.Application.Copilot;
using Microsoft.Extensions.Logging;

namespace AI.MIS.Infrastructure.Audit;

public sealed class LoggingAuditLogger(ILogger<LoggingAuditLogger> logger) : IAuditLogger
{
    public Task LogQueryAsync(QueryAuditEntry entry, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Copilot query audit. Outcome={Outcome}; Question={Question}; Sql={Sql}; ExecutionTimeMs={ExecutionTimeMs}; Error={Error}",
            entry.Outcome,
            entry.Question,
            entry.Sql,
            entry.ExecutionTimeMilliseconds,
            entry.Error);
        return Task.CompletedTask;
    }
}

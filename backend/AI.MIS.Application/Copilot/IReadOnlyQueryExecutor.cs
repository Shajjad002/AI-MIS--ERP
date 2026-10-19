using AI.MIS.Application.Copilot.Models;

namespace AI.MIS.Application.Copilot;

public interface IReadOnlyQueryExecutor
{
    Task<CopilotQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default);
}

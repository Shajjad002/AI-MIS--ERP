using AI.MIS.Application.Copilot.Models;

namespace AI.MIS.Application.Copilot;

public interface ICopilotPlanningService
{
    Task<CopilotInterpretation> InterpretAsync(string question, CancellationToken cancellationToken = default);
}

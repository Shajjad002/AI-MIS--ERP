using AI.MIS.Domain.Entities;

namespace AI.MIS.Application.Copilot.Interface
{
    public interface IProgramService
    {
        Task<ProgramInfo?> GetProgram(string programCode);
    }
}

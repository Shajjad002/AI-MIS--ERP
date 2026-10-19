using AI.MIS.Application.Copilot.Models;
using AI.MIS.Application.Copilot.SPModels;
using AI.MIS.Domain.Entities;

namespace AI.MIS.Application.Copilot.Interface;

public interface IDayInformationService : IBaseService<DayInformation>
{
    Task<IList<WorkStationDayStatusSP>> GetWorkStationDayStatusList(BranchDayStatusViewModel viewModel);
}

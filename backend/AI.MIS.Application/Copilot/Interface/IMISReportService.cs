using System;
using AI.MIS.Application.Copilot.Interface;
using AI.MIS.Application.Copilot.Models;
using AI.MIS.Application.Copilot.SPModels;
using AI.MIS.Domain.Entities;

namespace AI.MIS.Application.Copilot;

public interface IMISReportService : IBaseService<LoanTransactionReportSP>
{
    Task<BaseResponse> GetBranchPortfolioReport(
        ReportParameterViewModel viewModel,
        CancellationToken cancellationToken = default);
    Task<decimal> GetTotalDisbursement(
        ReportParameterViewModel viewModel,
        CancellationToken cancellationToken = default);
}

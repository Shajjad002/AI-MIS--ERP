using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Interface;
using AI.MIS.Application.Copilot.Models;
using AI.MIS.Application.Users;
using AI.MIS.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace AI.MIS.Api.Controllers;

[ApiController]
[Route("api/misReports")]
[Authorize]
public sealed class MISReportController : ControllerBase
{
    private readonly UserSession _userSession;
    private readonly IMISReportService _mISReportService;
    private readonly IProgramService _programService;
    private readonly IDayInformationService _dayInformationService;

    public MISReportController(UserSession userSession, IMISReportService mISReportService, IProgramService programService, IDayInformationService dayInformationService)
    {
        _userSession = userSession;
        _mISReportService = mISReportService;
        _programService = programService;
        _dayInformationService = dayInformationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboardData([FromQuery] DashboardReportQuery query,[FromQuery] bool summaryOnly = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.FromDate) ||
            string.IsNullOrWhiteSpace(query.ToDate) ||
            string.IsNullOrWhiteSpace(query.ProgramCode) ||
            string.IsNullOrWhiteSpace(query.ViewBy))
        {
            return BadRequest(new { message = "FromDate, ToDate, ProgramCode, and ViewBy are required query parameters." });
        }

        if (!DateTime.TryParseExact(query.FromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            !DateTime.TryParseExact(query.ToDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return BadRequest(new { message = "FromDate and ToDate must use the yyyy-MM-dd format." });
        }

        var viewModel = new ReportParameterViewModel
        {
            FromDate = query.FromDate!,
            ToDate = query.ToDate!,
            ProgramCode = query.ProgramCode!,
            ViewBy = query.ViewBy!,
            ZoneCode = query.ZoneCode ?? "",
            RegionCode = query.RegionCode ?? "",
            AreaCode = query.AreaCode ?? "",
            BranchCode = query.BranchCode ?? ""
        };

        if (summaryOnly)
        {
            var totalDisbursement = await _mISReportService.GetTotalDisbursement(viewModel, cancellationToken);
            return Ok(new BaseResponse
            {
                IsSuccessful = true,
                Message = "Ok",
                Data = new
                {
                    BranchPortfolio = new { TotalDisbursement = totalDisbursement }
                },
                IsFromReportServer = OperationPolicies.WillReportLoadFromReportServerDB == 1
            });
        }

        if (viewModel.ProgramCode == null)
        {
            viewModel.ProgramCode = _userSession.ProgramCode;
        }

        //var program = await _programService.GetProgram(viewModel.ProgramCode);
        //if (program is null)
        //{
        //    return NotFound(new { message = $"Program '{viewModel.ProgramCode}' was not found or is inactive." });
        //}

        var branchDayStatusViewModel = new BranchDayStatusViewModel
        {
            ProgramCode = viewModel.ProgramCode,
            SystemDay = _userSession.WorkStationSystemDay == default
                ? DateTime.Today
                : _userSession.WorkStationSystemDay.Date,
            ZoneCode = viewModel.ZoneCode,
            RegionCode = viewModel.RegionCode,
            AreaCode = viewModel.AreaCode,
            BranchCode = viewModel.BranchCode
        };
        var reportTask = _mISReportService.GetBranchPortfolioReport(viewModel, cancellationToken);
        var workStationStatusTask = _dayInformationService.GetWorkStationDayStatusList(branchDayStatusViewModel);
        await Task.WhenAll(reportTask, workStationStatusTask);
        var result = await reportTask;
        result.IsFromReportServer = OperationPolicies.WillReportLoadFromReportServerDB == 1;
        var workStationStatusList = await workStationStatusTask;

        var dashboardViewModel = new DashboardViewModel();
        if (result.IsSuccessful || workStationStatusList.Count > 0)
        {
            dashboardViewModel.BranchPortfolio = (BranchPortfolioViewModel)result.Data;
            if (!string.IsNullOrEmpty(viewModel.BranchCode))
            {
                dashboardViewModel.CurrentDateBranchOpenCount = workStationStatusList.Where(s => s.BranchCode == viewModel.BranchCode).ToList().Count;
            }
            else if (!string.IsNullOrEmpty(viewModel.AreaCode))
            {
                dashboardViewModel.CurrentDateBranchOpenCount = workStationStatusList.Where(s => s.AreaCode == viewModel.AreaCode).ToList().Count;
            }
            else if (!string.IsNullOrEmpty(viewModel.RegionCode))
            {
                dashboardViewModel.CurrentDateBranchOpenCount = workStationStatusList.Where(s => s.RegionCode == viewModel.RegionCode).ToList().Count;
            }
            else if (!string.IsNullOrEmpty(viewModel.ZoneCode))
            {
                dashboardViewModel.CurrentDateBranchOpenCount = workStationStatusList.Where(s => s.ZoneCode == viewModel.ZoneCode).ToList().Count;
            }
            else
            {
                dashboardViewModel.CurrentDateBranchOpenCount = workStationStatusList.Count;

            }

        }
        return Ok(new BaseResponse
        {
            IsSuccessful = result.IsSuccessful,
            Message = result.Message,
            Data = dashboardViewModel,
            IsFromReportServer = result.IsFromReportServer
        });

    }


    public sealed record LoginRequest(string UserName, string Password);

    public sealed class DashboardReportQuery
    {
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public string? ProgramCode { get; set; }
        public string? ViewBy { get; set; }
        public string? ZoneCode { get; set; }
        public string? RegionCode { get; set; }
        public string? AreaCode { get; set; }
        public string? BranchCode { get; set; }
        public string? LendingType { get; set; }
    }
}

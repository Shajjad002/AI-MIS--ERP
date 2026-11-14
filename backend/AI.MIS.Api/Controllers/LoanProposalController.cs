using AI.MIS.Application.Copilot.Interface;
using AI.MIS.Application.Copilot.Models;
using AI.MIS.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace AI.MIS.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LoanProposalController : ControllerBase
    {
        private readonly ILoanProposalService _loanProposalService;

        public LoanProposalController(ILoanProposalService loanProposalService)
        {
            _loanProposalService = loanProposalService;
        }

        [HttpGet]
        public async Task<IActionResult> GetLoanProposalSummaryList(LoanProposalSearchModel viewModel)
        {
            var _response = new BaseResponse();
            var isBranchUser = User.IsInRole("Branch User");
            var assignedBranches = User.FindAll("branch_code")
                .Select(claim => claim.Value.Trim())
                .Where(code => code.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (isBranchUser && assignedBranches.Length == 0)
                return Forbid();

            if (assignedBranches.Length > 0)
            {
                var requestedBranches = (viewModel.BranchCode ?? string.Empty)
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (requestedBranches.Length > 0 && requestedBranches.Any(code => !assignedBranches.Contains(code, StringComparer.OrdinalIgnoreCase)))
                    return Forbid();

                viewModel.BranchCode = requestedBranches.Length == 0
                    ? string.Join(",", assignedBranches)
                    : string.Join(",", requestedBranches);
            }
            else if (isBranchUser)
            {
                return Forbid();
            }

            var isBranchScoped = isBranchUser || assignedBranches.Length > 0;
            if (!TryGetDateRange(viewModel.ProbableDisburseDateFrom, viewModel.ProbableDisburseDateTo, out var probableDisbursementDays) ||
                !TryGetDateRange(viewModel.ProposalCreateDateFrom, viewModel.ProposalCreateDateTo, out var proposalCreateDays))
                return BadRequest(new { message = "Date ranges must include valid yyyy-MM-dd from and to dates." });

            if (!isBranchScoped && viewModel.IsBranchLevel == null && probableDisbursementDays > 45)
            {
                return Ok(new BaseResponse()
                {
                    IsSuccessful = false,
                    Message = "You cannot see proposals more than 45 days without selecting proposed loan amount",
                });
            }
            if (!isBranchScoped && viewModel.IsBranchLevel == null && proposalCreateDays > 45)
            {
                return Ok(new BaseResponse()
                {
                    IsSuccessful = false,
                    Message = "You cannot see proposals more than 45 days without selecting proposed loan amount",
                });
            }
            var result = await _loanProposalService.GetLoanProposalSummaryList(viewModel);
            _response.IsSuccessful = true;
            _response.Message = "OK";
            _response.Data = result;
            return Ok(_response);

        }

        private static bool TryGetDateRange(string from, string to, out double days)
        {
            days = 0;
            if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
                return true;
            if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate) ||
                !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate) ||
                toDate < fromDate)
                return false;
            days = (toDate - fromDate).TotalDays;
            return true;
        }
    }
}

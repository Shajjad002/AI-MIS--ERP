using AI.MIS.Application.Copilot.Models;
using AI.MIS.Application.Copilot.SPModels;
using AI.MIS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.MIS.Application.Copilot.Interface
{
    public interface ILoanProposalService : IBaseService<LoanProposal>
    {
        Task<IList<LoanProposalSearchSP>> GetLoanProposalSummaryList(LoanProposalSearchModel viewModel);
    }
}

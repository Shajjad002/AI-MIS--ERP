using AI.MIS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.MIS.Application.Copilot.Models
{
    public class LoanProposalSearchModel : BaseViewModel
    {
        public string Center { get; set; }
        public string MemberName { get; set; }
        public string LoanProduct { get; set; }
        public string Status { get; set; }

        public string ProbableDisburseDateFrom { get; set; }
        public string ProbableDisburseDateTo { get; set; }
        public string ProposalCreateDateFrom { get; set; }
        public string ProposalCreateDateTo { get; set; }

        public string ZoneCode { get; set; }
        public string RegionCode { get; set; }
        public string AreaCode { get; set; }
        public string ApproverRole { get; set; }

        public bool? IsBranchLevel { get; set; }

        public decimal BranchLevelDisbursementRange { get; set; }
        public string LoanInstallmentType { get; set; }
        public string MemberRank { get; set; }

        public LoanProposalSearchModel()
        {
            BranchLevelDisbursementRange = OperationPolicies.BranchLevelDisbursementRange;
        }
    }
}

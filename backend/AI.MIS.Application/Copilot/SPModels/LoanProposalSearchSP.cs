using AI.MIS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.MIS.Application.Copilot.SPModels
{
    public class LoanProposalSearchSP : BaseEntity
    {

        public string ProgramCode { get; set; }
        public string BranchCode { get; set; }
        public string BranchName { get; set; }
        public string CenterCode { get; set; }
        public string CenterName { get; set; }
        public string CenterDay { get; set; }
        public string MemberNo { get; set; }
        public string MemberName { get; set; }
        public string LoanProposalCode { get; set; }
        public string LoanPurposeCode { get; set; }
        public string LoanPurposeName { get; set; }
        public string LoanProductCode { get; set; }
        public string LoanProductName { get; set; }
        public string LoanAccountNo { get; set; }
        public string LoanAccountStatus { get; set; }
        public string LoanProductCategoryCode { get; set; }
        public string LoanProductCategoryName { get; set; }
        public string LoanType { get; set; }
        public string ApproverRoleCode { get; set; }
        public string Approver2RoleCode { get; set; }
        public string Approver3RoleCode { get; set; }

        public int TermNo { get; set; }
        public decimal ProposedLoanAmount { get; set; }
        public decimal? SanctionAmount { get; set; }
        public DateTime? ProbableDisburseDate { get; set; }
        public DateTime ProposalDate { get; set; }
        public string Status { get; set; }
        public string MemberPhotoURL { get; set; }
        public string Gender { get; set; }
        public string CeilingNo { get; set; }
        public int IsMainLoanExist { get; set; }
        public string MemberRank { get; set; }
        [NotMapped]
        public decimal LoanSecurityAmount { get; set; }

        [NotMapped]
        public decimal LoanProcessingFee { get; set; }

        [NotMapped]
        public decimal HealthServiceRegistrationFee { get; set; }

        public string LoanInstallmentType { get; set; }
    }
}

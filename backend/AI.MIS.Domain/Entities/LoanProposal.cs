using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.MIS.Domain.Entities
{
    public partial class LoanProposal : BaseEntity
    {
        public string LoanProposalCode { get; set; }
        public string LeadNo { get; set; }
        public string MemberNo { get; set; }
        //public string LoanPurposeCode { get; set; }
        public string LoanSectorCode { get; set; }
        public string LoanProductCode { get; set; }
        public string SourceOfIncomeCode { get; set; }
        public string TenorCode { get; set; }
        public int TermNo { get; set; }
        public int PolicyTermNo { get; set; }
        public string CeilingNo { get; set; }
        public decimal ProposedLoanAmount { get; set; }
        public decimal SanctionAmount { get; set; }
        public DateTime ProbableDisburseDate { get; set; }
        public string MemberGuardianNoPrimary { get; set; }
        public string MemberGuardianNoSecondary { get; set; }
        public string MemberGuarantorNoMandatory { get; set; }
        public string MemberGuarantorNoOptional { get; set; }
        public string MemberBankCode { get; set; }
        public string MemberBankAccountNo { get; set; }
        public string MemberWalletNo { get; set; }
        public string Status { get; set; }
        public DateTime ProposalDate { get; set; }
        public string Remarks { get; set; }
        public string DisbursementMethod { get; set; }
        public int InstallmentDayOfMonth { get; set; }



        [NotMapped]
        public string LoanSectorName { get; set; }
        [NotMapped]
        public string LoanParentSectorCode { get; set; }
        [NotMapped]
        public string LoanParentSectorName { get; set; }
        [NotMapped]
        public int WeekNo { get; set; }
        [NotMapped]
        public string CenterDay { get; set; }
        [NotMapped]
        public string CenterCode { get; set; }
        [NotMapped]
        public string MemberName { get; set; }
        [NotMapped]
        public string MemberRank { get; set; }
        [NotMapped]
        public string MemberStatus { get; set; }
        [NotMapped]
        public string LoanPurposeName { get; set; }
        [NotMapped]
        public string LoanProductName { get; set; }
        [NotMapped]
        public decimal LoanRoundAmount { get; set; }
        [NotMapped]
        public int NumberOfInstallment { get; set; }
        [NotMapped]
        public int DurationInMonth { get; set; }
        [NotMapped]
        public decimal InterestRate { get; set; }
        [NotMapped]
        public string LoanInstallmentTypeCode { get; set; }
        [NotMapped]
        public string LoanInstallmentTypeName { get; set; }
        [NotMapped]
        public decimal CeilingFrom { get; set; }
        [NotMapped]
        public decimal CeilingTo { get; set; }
        [NotMapped]
        public decimal? MandatoryDepositAmount { get; set; }
        [NotMapped]
        public bool? MDAInPercentage { get; set; }
        [NotMapped]
        public string MemberAccountNo { get; set; }
        [NotMapped]
        public string MemberAccountName { get; set; }
        [NotMapped]
        public string WalletNo { get; set; }
        [NotMapped]
        public string WalletType { get; set; }
        [NotMapped]
        public string MandatoryGuarantorName { get; set; }
        public string BranchCode { get; set; }
        [NotMapped]
        public string BranchName { get; set; }
        [NotMapped]
        public string AreaCode { get; set; }
        [NotMapped]
        public string RegionCode { get; set; }
        [NotMapped]
        public string ZoneCode { get; set; }
        [NotMapped]
        public string ProgramCode { get; set; }
        [NotMapped]
        public int InstallmentGracePeriod { get; set; }
        [NotMapped]
        public int InstallmentGracePeriodMax { get; set; }
        [NotMapped]
        public string MemberLendingType { get; set; }
        [NotMapped]
        public string LoanCategoryCode { get; set; }
        [NotMapped]
        public string LoanProductCategoryName { get; set; }
        [NotMapped]
        public decimal InstallmentAmount { get; set; }
        [NotMapped]
        public string SourceOfIncomeName { get; set; }

        [NotMapped]
        public int ApproverNo { get; set; }
        [NotMapped]
        public string ApproverRoleCode { get; set; }
        [NotMapped]
        public List<string> ChangeRequestFor { get; set; }

        [NotMapped]
        public string LeadGPSAddress { get; set; }
        [NotMapped]
        public bool IsItFromNewProposalTable { get; set; }

        [NotMapped]
        public bool IsCenterMemberAsRecommender { get; set; }

        [NotMapped]
        public string InstallmentPaymentInterval { get; set; }

        [NotMapped]
        public bool IsChangeAble { get; set; }

        [NotMapped]
        public bool IsProposalForRefinancedLoan { get; set; }

    }
}

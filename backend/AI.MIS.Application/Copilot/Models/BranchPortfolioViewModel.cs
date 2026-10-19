using System;
using AI.MIS.Application.Copilot.SPModels;

namespace AI.MIS.Application.Copilot.Models;

public class BranchPortfolioViewModel {
    public IList<BranchProtfolioSP> BranchProtfolioList { get; set; }
    public IList<BranchProtfolioSP> AreaWiseBranchProtfolioList { get; set; }
    public IList<BranchProtfolioSP> RegionWiseBranchProtfolioList { get; set; }
    public IList<BranchProtfolioSP> ZoneWiseBranchProtfolioList { get; set; }

    public int TotalZone { get; set; }
    public int TotalRegion { get; set; }
    public int TotalArea { get; set; }
    public int TotalBranch { get; set; }
    public int TotalNoOfOfficer { get; set; }
    public int TotalNoOfCreditOfficer { get; set; }
    public int TotalNoOfSMEOfficer { get; set; }
    public int TotalNoOfCenter { get; set; }
    public int TotalMember { get; set; }
    public int TotalBorrower { get; set; }
    public int TotalOverdueBorrower { get; set; }
    public decimal TotalDisbursement { get; set; }
    public decimal TotalOutstandingWithServiceCharge { get; set; }
    public decimal TotalOverdueWithServiceCharge { get; set; }
    public decimal TotalPAR { get; set; }
    public decimal TotalSavingsBalance { get; set; }
    public decimal TotalPrincipalOutstanding { get; set; }
    public decimal TotalServiceChargeOutstanding { get; set; }
    public decimal TotalOverduePrincipal { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal AvarageOTR { get; set; }
    public decimal AvaragePARRatio { get; set; }
    public decimal AvarageOverdueOutstandingRatio { get; set; }
    public decimal AvarageSavingsOutstandingRatio { get; set; }
    public decimal TotalBadLoanPrincipalOS { get; set; }
    public decimal AvarageGoodLoanPrincipalOSRatio { get; set; }
    public decimal TotalRecoverableAmount { get; set; }
    public decimal TotalRegulerRecovered { get; set; }
    public string LendingType { get; set; }
    public string ViewBy { get; set; }
    public string DataGenerationTime { get; set; }

}

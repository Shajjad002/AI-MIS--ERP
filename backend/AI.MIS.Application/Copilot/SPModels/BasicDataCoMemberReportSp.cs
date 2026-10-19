using System;
using System.ComponentModel.DataAnnotations.Schema;
using AI.MIS.Domain.Entities;

namespace AI.MIS.Application.Copilot.SPModels;

public class BasicDataCoMemberReportSp : BaseEntity
{
    public string ProgramCode { get; set; }
    public string ZoneCode { get; set; }
    public string ZoneName { get; set; }
    public string RegionCode { get; set; }
    public string RegionName { get; set; }
    public string AreaCode { get; set; }
    public string AreaName { get; set; }
    public string BranchCode { get; set; }
    public string BranchName { get; set; }
    public int TotalCO { get; set; }
    public int TotalMember { get; set; }
    [NotMapped]
    public int TotalODMember { get; set; }
    [NotMapped]
    public decimal OverdueBalance { get; set; }
    [NotMapped]
    public decimal PAR { get; set; }
    [NotMapped]
    public decimal TotalDisburse { get; set; }
    [NotMapped]
    public int TotalLoanee { get; set; }
    [NotMapped]
    public decimal TotalOutstanding { get; set; }
    [NotMapped]
    public decimal TotalSavings { get; set; }
    [NotMapped]
    public decimal PARRatio { get; set; }
    [NotMapped]
    public decimal ODOSRatio { get; set; }
    [NotMapped]
    public decimal SavingsOSRatio { get; set; }
    [NotMapped]
    public decimal OTR { get; set; }
}

public class BranchProtfolioSP
{
    public string ZoneCode { get; set; }
    public string ZoneName { get; set; }
    public string RegionCode { get; set; }
    public string RegionName { get; set; }
    public string AreaCode { get; set; }
    public string AreaName { get; set; }
    public string BranchCode { get; set; }
    public string BranchName { get; set; }
    [NotMapped]
    public int NoOfOfficer { get; set; }
    public int NoOfCreditOfficer { get; set; }
    public int NoOfSMEOfficer { get; set; }
    public int NoOfCenter { get; set; }
    public int Member { get; set; }
    public int Borrower { get; set; }
    public int OverdueBorrower { get; set; }
    public decimal Disbursement { get; set; }
    public decimal OutstandingWithServiceCharge { get; set; }
    public decimal OverdueWithServiceCharge { get; set; }
    public decimal PAR { get; set; }
    public decimal SavingsBalance { get; set; }
    public decimal PrincipalOutstanding { get; set; }
    public decimal ServiceChargeOutstanding { get; set; }
    public decimal OverduePrincipal { get; set; }
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal OTR { get; set; }
    public decimal PARRatio { get; set; }
    public decimal OverdueOutstandingRatio { get; set; }
    public decimal SavingsOutstandingRatio { get; set; }
    public decimal BadLoanPrincipalOS { get; set; }
    public decimal GoodLoanPrincipalOSRatio { get; set; }
    public decimal RecoverableAmount { get; set; }
    public decimal RegulerRecovered { get; set; }


    public string Zone => ZoneName + "(" + ZoneCode + ")";
    public string Region => RegionName + "(" + RegionCode + ")";
    public string Area => AreaName + "(" + AreaCode + ")";
    public string Branch => BranchName + "(" + BranchCode + ")";


}

public class DashboardDataSP
{
    public IList<BranchProtfolioSP> BranchProtfolioSP { get; set; }
    public string DataGenerationTime { get; set; }
    public string ErrorMessage { get; set; }

}
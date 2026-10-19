using System;
using System.ComponentModel.DataAnnotations.Schema;
using AI.MIS.Domain.Entities;

namespace AI.MIS.Application.Copilot.SPModels;

 public class LoanTransactionReportSP : BaseEntity
 {
     public string AccountNo { get; set; }
     public string AccountName { get; set; }
     public string AccountStatus { get; set; }
     public string TransactionNo { get; set; }
     public decimal InstallmentAmount { get; set; }
     public decimal TransactionAmount { get; set; }

     public DateTime TransactionDate { get; set; }
     public DateTime LoanExpireDate { get; set; }
     public DateTime DisburseDate { get; set; }
     public string TransactionMethod { get; set; }
     public string OperationType { get; set; }
     //public string RequestNo { get; set; }
     public string Remarks { get; set; }

     public decimal AdvanceAmount { get; set; }
     public decimal AdvanceAdjustmentAmount { get; set; }
     public decimal OverdueAmount { get; set; }
     public decimal OverdueRecovered { get; set; }
     public decimal PrincipalAmount { get; set; }
     public decimal InterestAmount { get; set; }

     public string ProgramCode { get; set; }
     public string BranchCode { get; set; }
        
     public string ZoneCode { get; set; }
     public string RegionCode { get; set; }
     public string AreaCode { get; set; }
     public string WorkStationNo { get; set; }
     public string LoanProductName { get; set; }
     public string LoanProductCode { get; set; }
     public string CenterCode { get; set; }
     public string CenterName { get; set; }
     public string CenterDay { get; set; }
     public string MemberNo { get; set; }
     public string MemberName { get; set; }
     public string ContactNo { get; set; }
     public int TermNo { get; set; }
     public string LoanPurposeCode { get; set; }
     public string LoanPurposeName { get; set; }
     public decimal CashAmount { get; set; }
     public decimal NonCashAmount { get; set; }
     public decimal RebateAmount { get; set; }
     public int PaidInstallmentNo { get; set; }
     public decimal OverduePrincipal { get; set; }
     public decimal OverdueInterest { get; set; }
     public decimal OverduePrincipalRecovered { get; set; }
     public decimal OverdueInterestRecovered { get; set; }
     public decimal RecoverablePrincipal { get; set; }
     public decimal RecoverableInterest { get; set; }

     [NotMapped]
     public decimal RecoverableAmount => RecoverablePrincipal + RecoverableInterest;

     [NotMapped]
     public string BranchName { get; set; }
     
     [NotMapped]
     public string LoanInstallmentType { get; set; }
     [NotMapped]
     public decimal TotalInterestAmount { get; set; }
     [NotMapped]
     public decimal ClosableAmount { get; set; }
     [NotMapped]
     public decimal TotalClosableAmount { get; set; }

 }

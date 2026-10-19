using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI.MIS.Application.Copilot.Models;

public class ReportParameterViewModel : BaseViewModel
{
    public string WorkstationNo { get; set; }
    public DateTime? WorkstationSystemDate { get; set; }
    public string AccountNo { get; set; }
    public string BranchName { get; set; }
    public string MemberName { get; set; }
    public string OperationType { get; set; }
    public string TransactionNo { get; set; }
    public string LoanProductCode { get; set; }
    public string DepositProductCode { get; set; }
    public string WorkStationType { get; set; }
    public decimal CashReceive { get; set; }
    public decimal CashPayment { get; set; }
    public decimal BankReceive { get; set; }
    public string SystemDate { get; set; }
    public string CollectionStatus { get; set; }
    public string OperationTypeforBoth { get; set; }
    public string Status { get; set; }
    public string ODClass { get; set; }
    public string ParentAccountNo { get; set; }
    public string LastClosedDate { get; set; }
    public DateTime? LastCloseDay { get; set; }
    public DateTime? LastOpenDay { get; set; }
    public string WorkStation { get; set; }
    public string BankAdviceStatus { get; set; }
    public string LendingType { get; set; }
    public string OverdueIncreaseOrDecrease { get; set; }
    public string OpeningBalance { get; set; }
    public string OverdueStatus { get; set; }
    public string OfficerEIN { get; set; }
    public int IgnoreYearClose { get; set; }
    public string CategoryOnTimeExtension { get; set; }
    public string LoanInstallmentType { get; set; }
    public string Column2FromDate { get; set; }
    public string Column2ToDate { get; set; }
    public string Column3FromDate { get; set; }
    public string Column3ToDate { get; set; }
    public string Layer { get; set; }
    public string Format { get; set; }

    [NotMapped]
    public string BranchCode { get; set; }
}

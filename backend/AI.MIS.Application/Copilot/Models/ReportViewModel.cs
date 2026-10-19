using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.MIS.Application.Copilot.Models
{
    public class ReportViewModel : BaseViewModel
    {
        public string OrganizationName => "Shakti Foundation for Disadvantaged Women";
        //public string OrganizationLogoURL => "";    
        public string OrganizationLogoURL { get; set; }
        public string WorkstationNo { get; set; }
        public string WorkstationName { get; set; }
        public string ProgramName { get; set; }
        public string BranchName { get; set; }
        public string ScreenCode { get; set; }
        public string ScreenName { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public string AccountNo { get; set; }
        public string AccountName { get; set; }
        public string AccountType { get; set; }
        public string AccountLevel { get; set; }
        public bool IsLeaf { get; set; }
        public string BankAccountNo { get; set; }
        public string BankAccountName { get; set; }
        public string CashInChequeAccountNo { get; set; }
        public string CashInChequeAccountName { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal BankOpeningBalance { get; set; }
        public decimal CashInChequeOpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal BankClosingBalance { get; set; }
        public decimal CashInChequeClosingBalance { get; set; }
        public decimal TotalOpening { get; set; }
        public decimal TotalClosing { get; set; }
        public decimal Balance { get; set; }
        public object Data { get; set; }
        public object Income { get; set; }
        public object Expenditure { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenditure { get; set; }
        public int TotalDisbursementCount { get; set; }
        public decimal Excess { get; set; }
        public List<Receipt> ReceiptList { get; set; }
        public List<Payment> PaymentList { get; set; }
        public List<ConsolidatedReceiptPayment> ConsolidatedReceipt { get; set; }
        public List<ConsolidatedReceiptPayment> ConsolidatedPayment { get; set; }
        public decimal TotalReceipt { get; set; }
        public decimal TotalPayment { get; set; }
        public decimal GrandTotalReceipt { get; set; }
        public decimal GrandTotalPayment { get; set; }
        public List<TrialBalance> Asset { get; set; }
        public List<TrialBalance> Liabilities { get; set; }
        public List<TrialBalance> Expense { get; set; }
        public List<TrialBalance> Revenue { get; set; }
        public List<TrialBalance> Equity { get; set; }
        public string CenterName { get; set; }
        public int WeekNo { get; set; }

        public string InstallmentPaymentInterval { get; set; }
        public string MeetingDay { get; set; }
        public string CreditOfficer { get; set; }
        public decimal TotalOverdueBalance { get; set; }
        public decimal TotalPARBalance { get; set; }
        public decimal TotalLoanOutstandingPrincipal { get; set; }
        public DateTime WorkStationSystemDate { get; set; }

        public Object FundTransferRequestList { get; set; }
        public Object FundTransferRequestSPList { get; set; }
        public bool IsFromReportServer { get; set; }
        public string DataGenerationTime { get; set; }
        public string ErrorMessage { get; set; }
        public string EmployeeName { get; set; }
        public string VoucherType { get; set; }
        public class ConsolidatedReceiptPayment
        {
            public string AccountNo { get; set; }
            public string AccountName { get; set; }
            public decimal ReceiptAmount { get; set; }
            public decimal PaymentAmount { get; set; }
            public string AccountTypeCode { get; set; }
        }



        public class Receipt
        {
            public string AccountNo { get; set; }
            public string AccountName { get; set; }
            public decimal ReceiptAmount { get; set; }
            //public decimal PaymentAmount { get; set; }
        }

        public class Payment
        {
            public string AccountNo { get; set; }
            public string AccountName { get; set; }
            //public decimal ReceiptAmount { get; set; }
            public decimal PaymentAmount { get; set; }
        }

        public class TrialBalance
        {
            public string WorkStationNo { get; set; }
            public string AccountNo { get; set; }
            public string AccountName { get; set; }
            public decimal Balance { get; set; }
            public int AccountLevel { get; set; }
            public string AccountTypeCode { get; set; }
            public bool IsLeaf { get; set; }
            public decimal DebitAmount { get; set; }
            public decimal CreditAmount { get; set; }

        }

        public decimal Sum(Func<object, object> p)
        {
            throw new NotImplementedException();
        }
    }
}

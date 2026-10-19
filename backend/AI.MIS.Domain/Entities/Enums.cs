using System;
using System.ComponentModel;

namespace AI.MIS.Domain.Entities;

  public static class OperationPolicies
  {
      public static decimal BranchLevelDisbursementRange; // => 500001;
      public static decimal CashDisbursementRange; // => 99999;
      public static int LastDisburseDateForGroupLoan; // => 25;
      public static int LastDisburseDateForIndividualLoan; // => 21;
      public static decimal BKashCharge; // => 1;
      public static bool BKashChargeInPercentage; // => true;
      public static int WillReportLoadFromReportServerDB; // => 1;
      public static DateTime DuplicatePassbookIssuePermissionDate; // => '2024-01-18';
      public static string DoubleSavingDuration; // 78;
      public static string ExcludeMashiKMunafaTenor; // 2311090001;
  };
public class Enums
{

    

}
public class ProgramList
{
    public const string Corporate = "000";
    public const string Microcredit = "001";
    public const string SME = "002";
    public const string Health = "003";
    public const string ProvidentFund = "006";
    public const string Enrich = "010";
    public const string AssetBusinessDevelopmentProject = "009";
    public const string ShaktiPharma = "011";
}

public static class MemberLendingType
{
    public static string Individual => "Individual";
    public static string Group => "Group";
};

public enum MemberStatus
{
    [Description("Active")] Active,
    [Description("Inactive")] Inactive,
    [Description("Dropout")] Dropout,
    [Description("Death")] Death,
    [Description("WriteOff")] WriteOff,
    [Description("Blacklisted")] Blacklisted,
    [Description("Pending")] Pending,
}
   public enum MemberTransferStatus
   {
       [Description("Transfer In")] TransferIn,
       [Description("Transfer Out")] TransferOut,
   }

   public enum LoanProposalStatus
   {
       [Description("Initial")]
       Initial,
       [Description("Pending")]
       Pending,
       [Description("Recommended")]
       Recommended,
       [Description("Approved")]
       Approved,
       [Description("Rejected")]
       Rejected,
       [Description("Open")]
       AccountOpened,
       [Description("Closed")]
       Closed,
       [Description("Disbursed")]
       Disbursed,
       [Description("Expired")]
       Expired
   };

   public enum DepositProposalStatus
   {
       [Description("Pending")]
       Pending,
       [Description("Rejected")]
       Rejected,
       [Description("Open")]
       AccountOpened,
       [Description("Closed")]
       Closed,
       [Description("Settled")]
       Settled

   };
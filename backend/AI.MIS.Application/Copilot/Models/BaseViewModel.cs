using System;

namespace AI.MIS.Application.Copilot.Models;

 public class BaseViewModel
 {
     public string FromDate { get; set; }
     public string ToDate { get; set; }
     public string Year { get; set; }
     public string Month { get; set; }
     public string ProgramCode { get; set; }
     public string DivisionCode { get; set; }
     public string ZoneCode { get; set; }
     public string RegionCode { get; set; }
     public string AreaCode { get; set; }
     public string BranchCode { get; set; }
     public string CenterCode { get; set; }
     public string CenterNo { get; set; }
     public string MemberNo { get; set; }
     public string LeadNo { get; set; }
     public string SetBy { get; set; }
     public string EIN { get; set; }
     public string CenterDay { get; set; }
     public string ViewBy { get; set; }
     public string InitialDate { get; set; }
     public int PageSize { get; set; } = 100000;
     public int PageNo { get; set; } = 1;
 }
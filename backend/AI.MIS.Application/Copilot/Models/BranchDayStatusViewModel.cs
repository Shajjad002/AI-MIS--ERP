using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI.MIS.Application.Copilot.Models;

public class BranchDayStatusViewModel
{
    public string ProgramCode { get; set; }
    public string ZoneCode { get; set; }
    public string RegionCode { get; set; }
    public string AreaCode { get; set; }
    public string BranchCode { get; set; }
    public DateTime? SystemDay { get; set; }
    [NotMapped]
    public string DayStatus { get; set; }
    [NotMapped]
    public string IssueNo { get; set; }
}

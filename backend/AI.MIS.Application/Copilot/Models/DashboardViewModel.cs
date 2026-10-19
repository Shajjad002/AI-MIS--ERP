using System;

namespace AI.MIS.Application.Copilot.Models;

public class DashboardViewModel
{
    public BranchPortfolioViewModel BranchPortfolio { get; set; }
    public int CurrentDateBranchOpenCount { get; set; }
}

using AI.MIS.Infrastructure;
using AI.MIS.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReportingInfrastructure(builder.Configuration);
builder.Services.AddPlatformService(builder.Configuration,
    "MISReportController",
    "ExcelImportsController",
    "LoanProposalController",
    "SystemController");

var app = builder.Build().UsePlatformService("AI.MIS.ReportingService");
app.Run();

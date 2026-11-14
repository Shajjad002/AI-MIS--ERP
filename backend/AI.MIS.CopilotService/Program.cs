using AI.MIS.Application.Common;
using AI.MIS.Infrastructure;
using AI.MIS.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddCopilotInfrastructure(builder.Configuration);
builder.Services.AddPlatformService(builder.Configuration,
    "CopilotController",
    "SchemaMetadataController");

var app = builder.Build().UsePlatformService("AI.MIS.CopilotService");
app.Run();

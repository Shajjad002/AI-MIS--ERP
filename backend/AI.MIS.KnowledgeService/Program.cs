using AI.MIS.Infrastructure;
using AI.MIS.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddKnowledgeInfrastructure(builder.Configuration);
builder.Services.AddPlatformService(builder.Configuration, "DocumentsController");

var app = builder.Build().UsePlatformService("AI.MIS.KnowledgeService");
app.Run();

using AI.MIS.Infrastructure;
using AI.MIS.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddPlatformService(builder.Configuration,
    "AuthController",
    "UserManagementController",
    "MenuManagementController");

var app = builder.Build().UsePlatformService("AI.MIS.IdentityService");
app.Run();

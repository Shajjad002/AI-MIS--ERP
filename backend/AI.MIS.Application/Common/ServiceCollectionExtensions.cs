using Microsoft.Extensions.DependencyInjection;
using AI.MIS.Application.Copilot;

namespace AI.MIS.Application.Common;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICopilotPlanningService, CopilotPlanningService>();
        return services;
    }
}

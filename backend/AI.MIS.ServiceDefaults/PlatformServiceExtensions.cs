using System.Security.Claims;
using System.Text;
using AI.MIS.Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

namespace AI.MIS.ServiceDefaults;

public static class PlatformServiceExtensions
{
    public static IServiceCollection AddPlatformService(
        this IServiceCollection services,
        IConfiguration configuration,
        params string[] controllerNames)
    {
        services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new AssemblyPart(typeof(CopilotController).Assembly));
                manager.FeatureProviders.Add(new ControllerSelectionFeatureProvider(controllerNames));
            });

        var authentication = configuration.GetSection("Authentication");
        var signingKey = authentication["SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            throw new InvalidOperationException("Authentication:SigningKey must be configured with at least 32 characters for every API service.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authentication["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = authentication["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
            });
        services.AddAuthorization();
        services.AddCors(options => options.AddPolicy("web", policy =>
            policy.WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
                .AllowAnyHeader().AllowAnyMethod()));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("copilot", limiter =>
            {
                limiter.PermitLimit = 20;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
        });
        services.AddHealthChecks();
        return services;
    }

    public static WebApplication UsePlatformService(this WebApplication app, string serviceName)
    {
        app.UseCors("web");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Ok(new { service = serviceName, status = "ready" }));
        return app;
    }

    private sealed class ControllerSelectionFeatureProvider(IEnumerable<string> controllerNames)
        : IApplicationFeatureProvider<ControllerFeature>
    {
        private readonly HashSet<string> allowedNames = new(controllerNames, StringComparer.OrdinalIgnoreCase);

        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var controller in feature.Controllers.ToArray())
            {
                if (!allowedNames.Contains(controller.Name))
                    feature.Controllers.Remove(controller);
            }
        }
    }
}

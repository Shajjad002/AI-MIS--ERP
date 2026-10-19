using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Interface;
using AI.MIS.Infrastructure.AI;
using AI.MIS.Infrastructure.Database;
using AI.MIS.Infrastructure.Audit;
using AI.MIS.Infrastructure.Security;
using AI.MIS.Application.Users;
using AI.MIS.Application.Rag;
using AI.MIS.Infrastructure.Rag;
using AI.MIS.Application.Menus;
using AI.MIS.Infrastructure.Menus;
using AI.MIS.Infrastructure.MISReport;
using AI.MIS.Domain.Entities;

namespace AI.MIS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<AuthenticationOptions>(configuration.GetSection(AuthenticationOptions.SectionName));
        services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<IEmbeddingGenerator, OpenAiEmbeddingGenerator>(client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddSingleton<ISchemaMetadataProvider, InMemorySchemaMetadataProvider>();
        services.AddSingleton<ISqlQueryValidator, SqlQueryValidator>();
        services.AddSingleton<IQueryAuthorizationService, QueryAuthorizationService>();
        services.AddScoped<IReadOnlyQueryExecutor, SqlReadOnlyQueryExecutor>();
        services.AddSingleton<IAuditLogger, LoggingAuditLogger>();
        services.AddScoped<IUserRepository, SqlUserRepository>();
        services.AddScoped<IUserManagementService, SqlUserManagementService>();
        services.AddScoped<IMenuManagementService, SqlMenuManagementService>();
        services.AddScoped<UserSession>();
        services.AddScoped<IMISReportService, MISReportService>();
        services.AddScoped<IDayInformationService, DayInformationService>();
        services.AddScoped<IProgramService, ProgramService>();
        services.AddScoped<IAuthenticationService, JwtAuthenticationService>();
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));
        services.AddSingleton<IDocumentStore, LocalDocumentStore>();
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddSingleton<IVectorStore, LocalVectorStore>();
        services.AddScoped<IRagService, RagService>();
        return services;
    }
}

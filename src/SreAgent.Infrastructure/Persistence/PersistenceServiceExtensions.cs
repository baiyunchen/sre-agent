using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SreAgent.Application.Services;
using SreAgent.Framework.Contexts;
using SreAgent.Infrastructure.BackgroundJobs;
using SreAgent.Repository;
using SreAgent.Repository.Repositories;

namespace SreAgent.Infrastructure.Persistence;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            }));

        // Register repositories
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IAgentRunRepository, AgentRunRepository>();
        services.AddScoped<IToolInvocationRepository, ToolInvocationRepository>();
        services.AddScoped<IDiagnosticDataRepository, DiagnosticDataRepository>();
        services.AddScoped<ICheckpointRepository, CheckpointRepository>();
        services.AddScoped<IInterventionRepository, InterventionRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // Register application services
        services.AddScoped<IDiagnosticDataService, DiagnosticDataService>();
        services.AddScoped<ICheckpointService, CheckpointService>();
        services.AddScoped<IInterventionService, InterventionService>();
        services.AddScoped<ISessionRecoveryService, SessionRecoveryService>();
        services.AddScoped<IAuditService, AuditService>();

        // Register background services
        services.AddHostedService<DataCleanupService>();

        // Register PostgresContextStore as the IContextStore implementation
        services.AddScoped<IContextStore, PostgresContextStore>();

        return services;
    }
}

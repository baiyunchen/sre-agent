using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SreAgent.Application.Services;
using SreAgent.Framework.Abstractions;
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
        services.AddScoped<IApprovalRuleRepository, ApprovalRuleRepository>();
        services.AddScoped<ILlmSettingsRepository, LlmSettingsRepository>();

        // Register application services
        services.AddScoped<IDiagnosticDataService, DiagnosticDataService>();
        services.AddScoped<ICheckpointService, CheckpointService>();
        services.AddScoped<IInterventionService, InterventionService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<ILlmSettingsService, LlmSettingsService>();
        services.AddSingleton<ToolApprovalService>();
        services.AddSingleton<IToolApprovalChecker>(sp => sp.GetRequiredService<ToolApprovalService>());
        services.AddSingleton<IToolApprovalResolver>(sp => sp.GetRequiredService<ToolApprovalService>());
        services.AddScoped<ISessionRecoveryService, SessionRecoveryService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<ISessionStreamPublisher, SessionStreamPublisher>();
        services.AddSingleton<ISessionExecutionRegistry, SessionExecutionRegistry>();
        services.AddSingleton<IBackgroundSessionExecutor, BackgroundSessionExecutor>();
        services.AddScoped<PersistenceExecutionTracker>();
        services.AddScoped<IExecutionTracker>(sp => new StreamingExecutionTracker(
            sp.GetRequiredService<PersistenceExecutionTracker>(),
            sp.GetRequiredService<ISessionStreamPublisher>(),
            sp.GetRequiredService<IAgentRunRepository>(),
            sp.GetRequiredService<IToolInvocationRepository>()));

        // Register background services
        services.AddHostedService<DataCleanupService>();

        // Register PostgresContextStore as the IContextStore implementation
        services.AddScoped<IContextStore, PostgresContextStore>();

        return services;
    }
}

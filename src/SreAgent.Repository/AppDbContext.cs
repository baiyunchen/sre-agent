using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository;

public class AppDbContext : DbContext
{
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();
    public DbSet<AgentRunEntity> AgentRuns => Set<AgentRunEntity>();
    public DbSet<ToolInvocationEntity> ToolInvocations => Set<ToolInvocationEntity>();
    public DbSet<CheckpointEntity> Checkpoints => Set<CheckpointEntity>();
    public DbSet<InterventionEntity> Interventions => Set<InterventionEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<DiagnosticDataEntity> DiagnosticData => Set<DiagnosticDataEntity>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

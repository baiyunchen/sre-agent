using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Configurations;

public class AgentRunConfiguration : IEntityTypeConfiguration<AgentRunEntity>
{
    public void Configure(EntityTypeBuilder<AgentRunEntity> builder)
    {
        builder.ToTable("agent_runs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(e => e.AgentId).HasColumnName("agent_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.AgentName).HasColumnName("agent_name").HasMaxLength(255);
        builder.Property(e => e.Input).HasColumnName("input").HasColumnType("jsonb");
        builder.Property(e => e.Output).HasColumnName("output").HasColumnType("jsonb");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message");
        builder.Property(e => e.Confidence).HasColumnName("confidence").HasColumnType("decimal(3,2)");
        builder.Property(e => e.Finding).HasColumnName("finding").HasColumnType("jsonb");
        builder.Property(e => e.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.DurationMs).HasColumnName("duration_ms");

        builder.HasOne(e => e.Session)
            .WithMany(s => s.AgentRuns)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.SessionId);
    }
}

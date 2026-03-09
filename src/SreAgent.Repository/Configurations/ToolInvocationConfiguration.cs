using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Configurations;

public class ToolInvocationConfiguration : IEntityTypeConfiguration<ToolInvocationEntity>
{
    public void Configure(EntityTypeBuilder<ToolInvocationEntity> builder)
    {
        builder.ToTable("tool_invocations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.AgentRunId).HasColumnName("agent_run_id").IsRequired();
        builder.Property(e => e.ToolName).HasColumnName("tool_name").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Parameters).HasColumnName("parameters").HasColumnType("jsonb");
        builder.Property(e => e.Result).HasColumnName("result").HasColumnType("jsonb");
        builder.Property(e => e.ApprovalStatus).HasColumnName("approval_status").HasMaxLength(50);
        builder.Property(e => e.ApprovedBy).HasColumnName("approved_by").HasMaxLength(100);
        builder.Property(e => e.ApprovedAt).HasColumnName("approved_at");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message");
        builder.Property(e => e.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(e => e.ExecutedAt).HasColumnName("executed_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.DurationMs).HasColumnName("duration_ms");

        builder.HasOne(e => e.AgentRun)
            .WithMany(r => r.ToolInvocations)
            .HasForeignKey(e => e.AgentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.AgentRunId);
    }
}

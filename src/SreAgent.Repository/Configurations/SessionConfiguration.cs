using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<SessionEntity>
{
    public void Configure(EntityTypeBuilder<SessionEntity> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(e => e.AlertId).HasColumnName("alert_id").HasMaxLength(255);
        builder.Property(e => e.AlertName).HasColumnName("alert_name").HasMaxLength(255);
        builder.Property(e => e.AlertData).HasColumnName("alert_data").HasColumnType("jsonb");
        builder.Property(e => e.ServiceName).HasColumnName("service_name").HasMaxLength(255);
        builder.Property(e => e.ServiceMetadata).HasColumnName("service_metadata").HasColumnType("jsonb");
        builder.Property(e => e.CurrentAgentId).HasColumnName("current_agent_id").HasMaxLength(100);
        builder.Property(e => e.CurrentStep).HasColumnName("current_step").HasDefaultValue(0);
        builder.Property(e => e.ExecutionState).HasColumnName("execution_state").HasColumnType("jsonb");
        builder.Property(e => e.Diagnosis).HasColumnName("diagnosis").HasColumnType("jsonb");
        builder.Property(e => e.DiagnosisSummary).HasColumnName("diagnosis_summary");
        builder.Property(e => e.Confidence).HasColumnName("confidence").HasColumnType("decimal(3,2)");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.StartedAt).HasColumnName("started_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.AlertId);
        builder.HasIndex(e => e.CreatedAt);
    }
}

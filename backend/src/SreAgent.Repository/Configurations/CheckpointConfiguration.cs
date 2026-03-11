using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Configurations;

public class CheckpointConfiguration : IEntityTypeConfiguration<CheckpointEntity>
{
    public void Configure(EntityTypeBuilder<CheckpointEntity> builder)
    {
        builder.ToTable("checkpoints");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(e => e.CheckpointName).HasColumnName("checkpoint_name").HasMaxLength(255);
        builder.Property(e => e.StepNumber).HasColumnName("step_number");
        builder.Property(e => e.SystemMessage).HasColumnName("system_message");
        builder.Property(e => e.MessageIds).HasColumnName("message_ids").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.SessionState).HasColumnName("session_state").HasColumnType("jsonb");
        builder.Property(e => e.AgentState).HasColumnName("agent_state").HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(e => e.Session)
            .WithMany(s => s.Checkpoints)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SessionId, e.CreatedAt });
    }
}

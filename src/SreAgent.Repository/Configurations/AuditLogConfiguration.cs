using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EventDescription).HasColumnName("event_description");
        builder.Property(e => e.EventData).HasColumnName("event_data").HasColumnType("jsonb");
        builder.Property(e => e.Actor).HasColumnName("actor").HasMaxLength(50);
        builder.Property(e => e.ActorId).HasColumnName("actor_id").HasMaxLength(100);
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasOne(e => e.Session)
            .WithMany(s => s.AuditLogs)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SessionId, e.OccurredAt });
    }
}

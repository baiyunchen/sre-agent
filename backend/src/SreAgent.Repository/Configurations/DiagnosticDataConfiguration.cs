using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Configurations;

public class DiagnosticDataConfiguration : IEntityTypeConfiguration<DiagnosticDataEntity>
{
    public void Configure(EntityTypeBuilder<DiagnosticDataEntity> builder)
    {
        builder.ToTable("diagnostic_data");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(e => e.SourceType).HasColumnName("source_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.SourceName).HasColumnName("source_name").HasMaxLength(255);
        builder.Property(e => e.ToolInvocationId).HasColumnName("tool_invocation_id");
        builder.Property(e => e.LogTimestamp).HasColumnName("log_timestamp");
        builder.Property(e => e.Content).HasColumnName("content").IsRequired();
        builder.Property(e => e.StructuredFields).HasColumnName("structured_fields").HasColumnType("jsonb");
        builder.Property(e => e.Severity).HasColumnName("severity").HasMaxLength(20);
        builder.Property(e => e.Tags).HasColumnName("tags");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();

        builder.HasOne(e => e.Session)
            .WithMany(s => s.DiagnosticData)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SessionId, e.SourceType });
        builder.HasIndex(e => new { e.SessionId, e.LogTimestamp });
        builder.HasIndex(e => new { e.SessionId, e.Severity });
        builder.HasIndex(e => e.ExpiresAt);
    }
}

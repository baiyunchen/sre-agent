using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Configurations;

public class InterventionConfiguration : IEntityTypeConfiguration<InterventionEntity>
{
    public void Configure(EntityTypeBuilder<InterventionEntity> builder)
    {
        builder.ToTable("interventions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(e => e.Type).HasColumnName("type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb");
        builder.Property(e => e.IntervenedBy).HasColumnName("intervened_by").HasMaxLength(100);
        builder.Property(e => e.IntervenedAt).HasColumnName("intervened_at").IsRequired();

        builder.HasOne(e => e.Session)
            .WithMany(s => s.Interventions)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.SessionId);
    }
}

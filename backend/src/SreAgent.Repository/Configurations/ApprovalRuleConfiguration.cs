using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Configurations;

public class ApprovalRuleConfiguration : IEntityTypeConfiguration<ApprovalRuleEntity>
{
    public void Configure(EntityTypeBuilder<ApprovalRuleEntity> builder)
    {
        builder.ToTable("approval_rules");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ToolName).HasColumnName("tool_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.RuleType).HasColumnName("rule_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.ToolName);
    }
}

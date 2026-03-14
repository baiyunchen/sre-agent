using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Configurations;

public class LlmSettingsConfiguration : IEntityTypeConfiguration<LlmSettingsEntity>
{
    public void Configure(EntityTypeBuilder<LlmSettingsEntity> builder)
    {
        builder.ToTable("llm_settings");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ApiKey).HasColumnName("api_key");
        builder.Property(e => e.ModelsJson).HasColumnName("models_json");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}

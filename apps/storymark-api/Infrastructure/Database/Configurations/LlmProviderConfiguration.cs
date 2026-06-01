using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Llm;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class LlmProviderConfiguration : IEntityTypeConfiguration<LlmProvider>
{
    public void Configure(EntityTypeBuilder<LlmProvider> builder)
    {
        builder.ToTable("llm_providers");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Name).HasColumnName("name");
        builder.Property(p => p.Kind).HasColumnName("kind").HasConversion<string>();
        builder.Property(p => p.BaseUrl).HasColumnName("base_url");
        builder.Property(p => p.ApiKeyEncrypted).HasColumnName("api_key_encrypted");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => p.Name).HasDatabaseName("llm_providers_name").IsUnique();
    }
}

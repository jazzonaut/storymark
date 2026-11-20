using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Llm;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class LlmModelConfiguration : IEntityTypeConfiguration<LlmModel>
{
    public void Configure(EntityTypeBuilder<LlmModel> builder)
    {
        builder.ToTable("llm_models");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Label).HasColumnName("label");
        builder.Property(m => m.ProviderId).HasColumnName("provider_id");
        builder.Property(m => m.ModelName).HasColumnName("model_name");
        builder.Property(m => m.ReasoningEffort).HasColumnName("reasoning_effort");
        builder.Property(m => m.InputPerMillion).HasColumnName("input_per_million");
        builder.Property(m => m.OutputPerMillion).HasColumnName("output_per_million");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<LlmProvider>()
            .WithMany()
            .HasForeignKey(m => m.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.ProviderId).HasDatabaseName("llm_models_provider");
    }
}

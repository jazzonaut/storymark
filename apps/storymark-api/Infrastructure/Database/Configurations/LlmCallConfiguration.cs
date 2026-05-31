using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.LlmCalls;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class LlmCallConfiguration : IEntityTypeConfiguration<LlmCall>
{
    public void Configure(EntityTypeBuilder<LlmCall> builder)
    {
        builder.ToTable("llm_calls");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Task).HasColumnName("task");
        builder.Property(c => c.Provider).HasColumnName("provider");
        builder.Property(c => c.Model).HasColumnName("model");
        builder.Property(c => c.Prompt).HasColumnName("prompt");
        builder.Property(c => c.Response).HasColumnName("response");
        builder.Property(c => c.TokensIn).HasColumnName("tokens_in");
        builder.Property(c => c.TokensOut).HasColumnName("tokens_out");
        builder.Property(c => c.LatencyMs).HasColumnName("latency_ms");
        builder.Property(c => c.CostEstimate).HasColumnName("cost_estimate");
        builder.Property(c => c.BookId).HasColumnName("book_id");
        builder.Property(c => c.SectionIndex).HasColumnName("section_index");
        builder.Property(c => c.UsedFutureContent).HasColumnName("used_future_content").HasDefaultValue(false);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(c => new { c.Task, c.CreatedAt })
            .HasDatabaseName("llm_calls_task_time")
            .IsDescending(false, true);
    }
}

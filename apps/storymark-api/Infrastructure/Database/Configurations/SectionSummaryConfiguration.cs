using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class SectionSummaryConfiguration : IEntityTypeConfiguration<SectionSummary>
{
    public void Configure(EntityTypeBuilder<SectionSummary> builder)
    {
        builder.ToTable("section_summaries");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.BookId).HasColumnName("book_id");
        builder.Property(s => s.SectionIndex).HasColumnName("section_index");
        builder.Property(s => s.Summary).HasColumnName("summary");

        var eventsComparer = new ValueComparer<IReadOnlyList<SectionSummaryEvent>>(
            (a, b) => (a ?? new List<SectionSummaryEvent>()).SequenceEqual(b ?? new List<SectionSummaryEvent>()),
            v => v.Aggregate(0, (hash, e) => HashCode.Combine(hash, e.GetHashCode())),
            v => v.ToList());

        builder.Property(s => s.ImportantEvents)
            .HasColumnName("important_events")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<SectionSummaryEvent>>(v, JsonSerializerOptions.Default)
                    ?? new List<SectionSummaryEvent>(),
                eventsComparer);

        var stringsComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode(StringComparison.Ordinal))),
            v => v.ToList());

        builder.Property(s => s.EntitiesSeen)
            .HasColumnName("entities_seen")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
                stringsComparer);

        builder.Property(s => s.OpenThreads)
            .HasColumnName("open_threads")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
                stringsComparer);

        builder.Property(s => s.GeneratedAt).HasColumnName("generated_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(s => s.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.BookId, s.SectionIndex })
            .HasDatabaseName("section_summaries_book_section")
            .IsUnique();
    }
}

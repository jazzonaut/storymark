using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookEntitySummaryConfiguration : IEntityTypeConfiguration<BookEntitySummary>
{
    public void Configure(EntityTypeBuilder<BookEntitySummary> builder)
    {
        builder.ToTable("book_entity_summaries");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.BookId).HasColumnName("book_id");
        builder.Property(s => s.EntityId).HasColumnName("entity_id");
        builder.Property(s => s.UpToSection).HasColumnName("up_to_section");
        builder.Property(s => s.UpToLocation).HasColumnName("up_to_location");
        builder.Property(s => s.ShortSummary).HasColumnName("short_summary");

        var comparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode(StringComparison.Ordinal))),
            v => v.ToList());

        builder.Property(s => s.KnownFacts)
            .HasColumnName("known_facts")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
                comparer);

        builder.Property(s => s.Relationships)
            .HasColumnName("relationships")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
                comparer);

        builder.Property(s => s.OpenQuestions)
            .HasColumnName("open_questions")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
                comparer);

        builder.Property(s => s.GeneratedAt).HasColumnName("generated_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(s => s.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade from the entity too: re-extraction clears book_entities, which must
        // take its cached summaries with it (entity ids are regenerated).
        builder.HasOne<BookEntity>()
            .WithMany()
            .HasForeignKey(s => s.EntityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.EntityId, s.UpToSection, s.UpToLocation })
            .HasDatabaseName("book_entity_summaries_pos")
            .IsUnique();
    }
}

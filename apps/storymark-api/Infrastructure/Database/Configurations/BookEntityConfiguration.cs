using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookEntityConfiguration : IEntityTypeConfiguration<BookEntity>
{
    public void Configure(EntityTypeBuilder<BookEntity> builder)
    {
        builder.ToTable("book_entities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.BookId).HasColumnName("book_id");
        builder.Property(e => e.CanonicalName).HasColumnName("canonical_name");
        builder.Property(e => e.EntityType).HasColumnName("entity_type").HasConversion<string>();

        var comparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode(StringComparison.Ordinal))),
            v => v.ToList());

        builder.Property(e => e.Aliases)
            .HasColumnName("aliases")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
                comparer);

        builder.Property(e => e.FirstSeenSection).HasColumnName("first_seen_section");
        builder.Property(e => e.FirstSeenLocation).HasColumnName("first_seen_location");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(e => e.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.BookId, e.CanonicalName })
            .HasDatabaseName("book_entities_book_canonical")
            .IsUnique();
    }
}

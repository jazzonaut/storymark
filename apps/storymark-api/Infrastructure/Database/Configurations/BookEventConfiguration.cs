using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookEventConfiguration : IEntityTypeConfiguration<BookEvent>
{
    public void Configure(EntityTypeBuilder<BookEvent> builder)
    {
        builder.ToTable("book_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.BookId).HasColumnName("book_id");
        builder.Property(e => e.SectionIndex).HasColumnName("section_index");
        builder.Property(e => e.LocationStart).HasColumnName("location_start");
        builder.Property(e => e.LocationEnd).HasColumnName("location_end");
        builder.Property(e => e.EventType).HasColumnName("event_type");
        builder.Property(e => e.Description).HasColumnName("description");

        var comparer = new ValueComparer<IReadOnlyList<Guid>>(
            (a, b) => (a ?? new List<Guid>()).SequenceEqual(b ?? new List<Guid>()),
            v => v.Aggregate(0, (hash, id) => HashCode.Combine(hash, id.GetHashCode())),
            v => v.ToList());

        builder.Property(e => e.InvolvedEntityIds)
            .HasColumnName("involved_entity_ids")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<Guid>>(v, JsonSerializerOptions.Default) ?? new List<Guid>(),
                comparer);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(e => e.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.BookId, e.SectionIndex, e.LocationStart })
            .HasDatabaseName("book_events_pos");
    }
}

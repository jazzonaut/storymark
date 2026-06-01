using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookEntityMentionConfiguration : IEntityTypeConfiguration<BookEntityMention>
{
    public void Configure(EntityTypeBuilder<BookEntityMention> builder)
    {
        builder.ToTable("book_entity_mentions");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.BookId).HasColumnName("book_id");
        builder.Property(m => m.EntityId).HasColumnName("entity_id");
        builder.Property(m => m.SectionIndex).HasColumnName("section_index");
        builder.Property(m => m.LocationStart).HasColumnName("location_start");
        builder.Property(m => m.LocationEnd).HasColumnName("location_end");
        builder.Property(m => m.MentionText).HasColumnName("mention_text");
        builder.Property(m => m.SurroundingText).HasColumnName("surrounding_text");
        builder.Property(m => m.Confidence).HasColumnName("confidence");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(m => m.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BookEntity>()
            .WithMany()
            .HasForeignKey(m => m.EntityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.BookId, m.EntityId, m.SectionIndex })
            .HasDatabaseName("book_entity_mentions_entity_pos");
    }
}

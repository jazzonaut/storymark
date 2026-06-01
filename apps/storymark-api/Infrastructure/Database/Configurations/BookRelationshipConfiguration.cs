using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookRelationshipConfiguration : IEntityTypeConfiguration<BookRelationship>
{
    public void Configure(EntityTypeBuilder<BookRelationship> builder)
    {
        builder.ToTable("book_relationships");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.BookId).HasColumnName("book_id");
        builder.Property(r => r.FromEntityId).HasColumnName("from_entity_id");
        builder.Property(r => r.ToEntityId).HasColumnName("to_entity_id");
        builder.Property(r => r.RelationshipType).HasColumnName("relationship_type");
        builder.Property(r => r.Polarity).HasColumnName("polarity");
        builder.Property(r => r.Evidence).HasColumnName("evidence");
        builder.Property(r => r.FirstSeenSection).HasColumnName("first_seen_section");
        builder.Property(r => r.FirstSeenLocation).HasColumnName("first_seen_location");
        builder.Property(r => r.LastSeenSection).HasColumnName("last_seen_section");
        builder.Property(r => r.LastSeenLocation).HasColumnName("last_seen_location");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        // All three FKs cascade (plan §12.13): deleting a book removes its
        // relationships, and deleting an entity removes any edge it sits on either
        // end of. Postgres permits the resulting multiple cascade paths to books.
        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(r => r.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BookEntity>()
            .WithMany()
            .HasForeignKey(r => r.FromEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BookEntity>()
            .WithMany()
            .HasForeignKey(r => r.ToEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.BookId, r.FirstSeenSection })
            .HasDatabaseName("book_relationships_pos");
    }
}

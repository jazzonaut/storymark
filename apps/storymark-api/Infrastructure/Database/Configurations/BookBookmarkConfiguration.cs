using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookBookmarkConfiguration : IEntityTypeConfiguration<BookBookmark>
{
    public void Configure(EntityTypeBuilder<BookBookmark> builder)
    {
        builder.ToTable("book_bookmarks");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.BookId).HasColumnName("book_id");
        builder.Property(b => b.SectionIndex).HasColumnName("section_index");
        builder.Property(b => b.ParagraphIndex).HasColumnName("paragraph_index");
        builder.Property(b => b.LocationStart).HasColumnName("location_start");
        builder.Property(b => b.Label).HasColumnName("label");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(b => b.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.BookId).HasDatabaseName("book_bookmarks_book");
    }
}

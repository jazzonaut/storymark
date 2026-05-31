using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookSectionConfiguration : IEntityTypeConfiguration<BookSection>
{
    public void Configure(EntityTypeBuilder<BookSection> builder)
    {
        builder.ToTable("book_sections");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.BookId).HasColumnName("book_id");
        builder.Property(s => s.SectionIndex).HasColumnName("section_index");
        builder.Property(s => s.Title).HasColumnName("title");
        builder.Property(s => s.SourceHref).HasColumnName("source_href");
        builder.Property(s => s.HtmlPath).HasColumnName("html_path");
        builder.Property(s => s.TextPath).HasColumnName("text_path");
        builder.Property(s => s.TextHash).HasColumnName("text_hash");
        builder.Property(s => s.WordCount).HasColumnName("word_count").HasDefaultValue(0);
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(s => s.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.BookId, s.SectionIndex })
            .HasDatabaseName("book_sections_book_index")
            .IsUnique();
    }
}

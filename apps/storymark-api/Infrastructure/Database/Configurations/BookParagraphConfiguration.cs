using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookParagraphConfiguration : IEntityTypeConfiguration<BookParagraph>
{
    public void Configure(EntityTypeBuilder<BookParagraph> builder)
    {
        builder.ToTable("book_paragraphs");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.BookId).HasColumnName("book_id");
        builder.Property(p => p.SectionId).HasColumnName("section_id");
        builder.Property(p => p.SectionIndex).HasColumnName("section_index");
        builder.Property(p => p.ParagraphIndex).HasColumnName("paragraph_index");
        builder.Property(p => p.LocationStart).HasColumnName("location_start");
        builder.Property(p => p.LocationEnd).HasColumnName("location_end");
        builder.Property(p => p.Html).HasColumnName("html");
        builder.Property(p => p.PlainText).HasColumnName("plain_text");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        builder.HasOne<BookSection>()
            .WithMany()
            .HasForeignKey(p => p.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.BookId, p.SectionIndex, p.ParagraphIndex })
            .HasDatabaseName("book_paragraphs_book_section_index")
            .IsUnique();
    }
}

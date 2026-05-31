using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.ContentHash).HasColumnName("content_hash");
        builder.Property(b => b.Title).HasColumnName("title");
        builder.Property(b => b.Author).HasColumnName("author");
        builder.Property(b => b.Language).HasColumnName("language");
        builder.Property(b => b.Format).HasColumnName("format");
        builder.Property(b => b.FilePath).HasColumnName("file_path");
        builder.Property(b => b.CoverPath).HasColumnName("cover_path");
        builder.Property(b => b.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasConversion<string>();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(b => b.ContentHash)
            .HasDatabaseName("books_content_hash")
            .IsUnique();
    }
}

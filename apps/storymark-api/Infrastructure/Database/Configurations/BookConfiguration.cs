using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Users;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.ContentHash).HasColumnName("content_hash");
        builder.Property(b => b.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(b => b.IsPublic).HasColumnName("is_public");
        builder.Property(b => b.Title).HasColumnName("title");
        builder.Property(b => b.Author).HasColumnName("author");
        builder.Property(b => b.Language).HasColumnName("language");
        builder.Property(b => b.Format).HasColumnName("format");
        builder.Property(b => b.FilePath).HasColumnName("file_path");
        builder.Property(b => b.CoverPath).HasColumnName("cover_path");
        builder.Property(b => b.ProcessingRunId).HasColumnName("processing_run_id").IsConcurrencyToken();
        builder.Property(b => b.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasConversion<string>();
        builder.Property(b => b.ProcessingError).HasColumnName("processing_error");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        // Restrict, not cascade: deleting a user must never silently delete a
        // library — books are heavyweight shared content, unlike per-reader state.
        builder.HasOne<StorymarkUser>()
            .WithMany()
            .HasForeignKey(b => b.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Dedup is per owner (ADR-0023): two users may each hold a private copy
        // of the same content (separate rows, shared on-disk directory). Global
        // hash uniqueness would either entangle ownership or leak that someone
        // else privately owns the book.
        builder.HasIndex(b => new { b.ContentHash, b.OwnerUserId })
            .HasDatabaseName("books_content_hash_owner")
            .IsUnique();

        // VisibleTo's predicate is (owner OR public): the owner_user_id index
        // serves the first arm, this partial index the second, so the library
        // query keeps index support (a bitmap OR) as the catalogue grows.
        builder.HasIndex(b => b.IsPublic)
            .HasDatabaseName("books_public")
            .HasFilter("is_public");
    }
}

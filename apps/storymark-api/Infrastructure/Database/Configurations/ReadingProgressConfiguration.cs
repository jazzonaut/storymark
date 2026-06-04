using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Users;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class ReadingProgressConfiguration : IEntityTypeConfiguration<ReadingProgress>
{
    public void Configure(EntityTypeBuilder<ReadingProgress> builder)
    {
        builder.ToTable("reading_progress");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.BookId).HasColumnName("book_id");
        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.DeviceId).HasColumnName("device_id");
        builder.Property(p => p.SectionIndex).HasColumnName("section_index");
        builder.Property(p => p.ParagraphIndex).HasColumnName("paragraph_index");
        builder.Property(p => p.LocationStart).HasColumnName("location_start");
        builder.Property(p => p.LocationEnd).HasColumnName("location_end");
        builder.Property(p => p.ClientTimestamp).HasColumnName("client_timestamp");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(p => p.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<StorymarkUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.BookId, p.UserId, p.DeviceId })
            .HasDatabaseName("reading_progress_book_user_device")
            .IsUnique();

        // Matches GetLatestAsync: filter by book + user, take the newest by server
        // receive time. The unique index above shares the leading columns but
        // can't serve the UpdatedAt ordering.
        builder.HasIndex(p => new { p.BookId, p.UserId, p.UpdatedAt })
            .HasDatabaseName("reading_progress_book_user_updated")
            .IsDescending(false, false, true);
    }
}

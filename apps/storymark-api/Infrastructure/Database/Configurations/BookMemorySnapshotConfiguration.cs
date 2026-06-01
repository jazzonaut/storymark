using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookMemorySnapshotConfiguration : IEntityTypeConfiguration<BookMemorySnapshot>
{
    public void Configure(EntityTypeBuilder<BookMemorySnapshot> builder)
    {
        builder.ToTable("book_memory_snapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.BookId).HasColumnName("book_id");
        builder.Property(s => s.UpToSection).HasColumnName("up_to_section");
        builder.Property(s => s.UpToLocation).HasColumnName("up_to_location");
        builder.Property(s => s.SummarySoFar).HasColumnName("summary_so_far");

        var comparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode(StringComparison.Ordinal))),
            v => v.ToList());

        builder.Property(s => s.Timeline)
            .HasColumnName("timeline")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
                comparer);

        builder.Property(s => s.OpenThreads)
            .HasColumnName("open_threads")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
                comparer);

        builder.Property(s => s.GeneratedAt).HasColumnName("generated_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(s => s.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.BookId, s.UpToSection, s.UpToLocation })
            .HasDatabaseName("book_memory_snapshots_pos")
            .IsUnique();
    }
}

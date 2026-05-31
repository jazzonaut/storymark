using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class QuestionAnswerConfiguration : IEntityTypeConfiguration<QuestionAnswer>
{
    public void Configure(EntityTypeBuilder<QuestionAnswer> builder)
    {
        builder.ToTable("question_answers");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id");
        builder.Property(q => q.BookId).HasColumnName("book_id");
        builder.Property(q => q.Question).HasColumnName("question");
        builder.Property(q => q.Answer).HasColumnName("answer");
        builder.Property(q => q.CurrentSection).HasColumnName("current_section");
        builder.Property(q => q.CurrentLocation).HasColumnName("current_location");

        var comparer = new ValueComparer<IReadOnlyList<QuestionSource>>(
            (a, b) => (a ?? new List<QuestionSource>()).SequenceEqual(b ?? new List<QuestionSource>()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        builder.Property(q => q.Sources)
            .HasColumnName("sources")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<QuestionSource>>(v, JsonSerializerOptions.Default) ?? new List<QuestionSource>(),
                comparer);

        builder.Property(q => q.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(q => q.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => new { q.BookId, q.CreatedAt })
            .HasDatabaseName("question_answers_book_time")
            .IsDescending(false, true);
    }
}

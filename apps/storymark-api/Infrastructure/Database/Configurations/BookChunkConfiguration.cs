using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookChunkConfiguration : IEntityTypeConfiguration<BookChunk>
{
    // nomic-embed-text dimensionality (docs/plan.md §12.4); the column is created
    // now but stays null until the S3.2 embedding job. No HNSW/FTS index yet.
    private const int EmbeddingDimensions = 768;

    public void Configure(EntityTypeBuilder<BookChunk> builder)
    {
        builder.ToTable("book_chunks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.BookId).HasColumnName("book_id");
        builder.Property(c => c.SectionId).HasColumnName("section_id");
        builder.Property(c => c.SectionIndex).HasColumnName("section_index");
        builder.Property(c => c.ChunkIndex).HasColumnName("chunk_index");
        builder.Property(c => c.LocationStart).HasColumnName("location_start");
        builder.Property(c => c.LocationEnd).HasColumnName("location_end");
        builder.Property(c => c.Text).HasColumnName("text");
        builder.Property(c => c.TokenEstimate).HasColumnName("token_estimate");
        builder.Property(c => c.Embedding).HasColumnName("embedding").HasColumnType($"vector({EmbeddingDimensions})");
        builder.Property(c => c.EmbeddingModelId).HasColumnName("embedding_model_id");
        builder.Property(c => c.EmbeddingModelVersion).HasColumnName("embedding_model_version");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BookSection>()
            .WithMany()
            .HasForeignKey(c => c.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.BookId, c.SectionIndex, c.ChunkIndex })
            .HasDatabaseName("book_chunks_book_section_chunk")
            .IsUnique();

        builder.HasIndex(c => new { c.BookId, c.SectionIndex, c.LocationStart })
            .HasDatabaseName("book_chunks_pos");
    }
}

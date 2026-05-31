using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class BookChunkConfiguration : IEntityTypeConfiguration<BookChunk>
{
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
        // Dimensionless `vector` (not vector(768)): the embedding model is swappable
        // and different models have different dimensions (S3.4). Search only ever
        // compares vectors of one model (filtered by embedding_model_id), so dimensions
        // never clash at query time. No HNSW index — it requires a fixed dimension and
        // earns nothing at single-operator scale; an exact <=> scan is sub-ms here.
        builder.Property(c => c.Embedding).HasColumnName("embedding").HasColumnType("vector");
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

        // The GIN FTS index on to_tsvector('english', text) is an expression index EF
        // can't model — it's added via raw SQL in the AddBookChunkIndexes migration.
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBookChunkIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "book_chunks_hnsw",
                table: "book_chunks",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            // GIN full-text index over the chunk text (docs/plan.md §13). This is an
            // expression index on the immutable two-arg to_tsvector, which EF can't
            // model via the fluent API, so it's created in raw SQL and is invisible to
            // the model snapshot (EF won't try to manage or drop it).
            migrationBuilder.Sql(
                "CREATE INDEX book_chunks_fts ON book_chunks USING gin (to_tsvector('english', text));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS book_chunks_fts;");

            migrationBuilder.DropIndex(
                name: "book_chunks_hnsw",
                table: "book_chunks");
        }
    }
}

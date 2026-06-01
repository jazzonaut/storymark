using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBookEntitySummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "book_entity_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    up_to_section = table.Column<int>(type: "integer", nullable: false),
                    up_to_location = table.Column<int>(type: "integer", nullable: false),
                    short_summary = table.Column<string>(type: "text", nullable: false),
                    known_facts = table.Column<string>(type: "jsonb", nullable: false),
                    relationships = table.Column<string>(type: "jsonb", nullable: false),
                    open_questions = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_entity_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_entity_summaries_book_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "book_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_entity_summaries_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "book_entity_summaries_pos",
                table: "book_entity_summaries",
                columns: new[] { "entity_id", "up_to_section", "up_to_location" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_book_entity_summaries_book_id",
                table: "book_entity_summaries",
                column: "book_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_entity_summaries");
        }
    }
}

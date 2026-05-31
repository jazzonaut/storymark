using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBookSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "book_sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    source_href = table.Column<string>(type: "text", nullable: true),
                    html_path = table.Column<string>(type: "text", nullable: true),
                    text_path = table.Column<string>(type: "text", nullable: true),
                    text_hash = table.Column<string>(type: "text", nullable: true),
                    word_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_sections", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_sections_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_paragraphs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    paragraph_index = table.Column<int>(type: "integer", nullable: false),
                    location_start = table.Column<int>(type: "integer", nullable: false),
                    location_end = table.Column<int>(type: "integer", nullable: false),
                    html = table.Column<string>(type: "text", nullable: true),
                    plain_text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_paragraphs", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_paragraphs_book_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "book_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "book_paragraphs_book_section_index",
                table: "book_paragraphs",
                columns: new[] { "book_id", "section_index", "paragraph_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_book_paragraphs_section_id",
                table: "book_paragraphs",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "book_sections_book_index",
                table: "book_sections",
                columns: new[] { "book_id", "section_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_paragraphs");

            migrationBuilder.DropTable(
                name: "book_sections");
        }
    }
}

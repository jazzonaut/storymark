using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "book_entity_mentions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    location_start = table.Column<int>(type: "integer", nullable: false),
                    location_end = table.Column<int>(type: "integer", nullable: false),
                    mention_text = table.Column<string>(type: "text", nullable: false),
                    surrounding_text = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_entity_mentions", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_entity_mentions_book_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "book_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_entity_mentions_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    location_start = table.Column<int>(type: "integer", nullable: false),
                    location_end = table.Column<int>(type: "integer", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    involved_entity_ids = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_events_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_relationships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship_type = table.Column<string>(type: "text", nullable: false),
                    polarity = table.Column<string>(type: "text", nullable: true),
                    evidence = table.Column<string>(type: "text", nullable: true),
                    first_seen_section = table.Column<int>(type: "integer", nullable: false),
                    first_seen_location = table.Column<int>(type: "integer", nullable: false),
                    last_seen_section = table.Column<int>(type: "integer", nullable: true),
                    last_seen_location = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_relationships", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_relationships_book_entities_from_entity_id",
                        column: x => x.from_entity_id,
                        principalTable: "book_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_relationships_book_entities_to_entity_id",
                        column: x => x.to_entity_id,
                        principalTable: "book_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_relationships_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "section_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    important_events = table.Column<string>(type: "jsonb", nullable: false),
                    entities_seen = table.Column<string>(type: "jsonb", nullable: false),
                    open_threads = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_section_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_section_summaries_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "book_entity_mentions_entity_pos",
                table: "book_entity_mentions",
                columns: new[] { "book_id", "entity_id", "section_index" });

            migrationBuilder.CreateIndex(
                name: "IX_book_entity_mentions_entity_id",
                table: "book_entity_mentions",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "book_events_pos",
                table: "book_events",
                columns: new[] { "book_id", "section_index", "location_start" });

            migrationBuilder.CreateIndex(
                name: "book_relationships_pos",
                table: "book_relationships",
                columns: new[] { "book_id", "first_seen_section" });

            migrationBuilder.CreateIndex(
                name: "IX_book_relationships_from_entity_id",
                table: "book_relationships",
                column: "from_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_book_relationships_to_entity_id",
                table: "book_relationships",
                column: "to_entity_id");

            migrationBuilder.CreateIndex(
                name: "section_summaries_book_section",
                table: "section_summaries",
                columns: new[] { "book_id", "section_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_entity_mentions");

            migrationBuilder.DropTable(
                name: "book_events");

            migrationBuilder.DropTable(
                name: "book_relationships");

            migrationBuilder.DropTable(
                name: "section_summaries");
        }
    }
}

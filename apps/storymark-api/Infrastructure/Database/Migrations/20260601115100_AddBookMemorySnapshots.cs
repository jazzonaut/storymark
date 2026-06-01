using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBookMemorySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "book_memory_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    up_to_section = table.Column<int>(type: "integer", nullable: false),
                    up_to_location = table.Column<int>(type: "integer", nullable: false),
                    summary_so_far = table.Column<string>(type: "text", nullable: false),
                    timeline = table.Column<string>(type: "jsonb", nullable: false),
                    open_threads = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_memory_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_memory_snapshots_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "book_memory_snapshots_pos",
                table: "book_memory_snapshots",
                columns: new[] { "book_id", "up_to_section", "up_to_location" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_memory_snapshots");
        }
    }
}

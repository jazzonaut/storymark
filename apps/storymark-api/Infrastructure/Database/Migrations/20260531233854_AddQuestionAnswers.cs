using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "question_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    answer = table.Column<string>(type: "text", nullable: false),
                    current_section = table.Column<int>(type: "integer", nullable: false),
                    current_location = table.Column<int>(type: "integer", nullable: false),
                    sources = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_question_answers_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "question_answers_book_time",
                table: "question_answers",
                columns: new[] { "book_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_answers");
        }
    }
}

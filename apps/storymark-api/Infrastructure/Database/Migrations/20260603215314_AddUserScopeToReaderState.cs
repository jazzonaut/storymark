using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserScopeToReaderState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "reading_progress_book_device",
                table: "reading_progress");

            migrationBuilder.DropIndex(
                name: "reading_progress_book_updated",
                table: "reading_progress");

            migrationBuilder.DropIndex(
                name: "book_bookmarks_book",
                table: "book_bookmarks");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "reading_progress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "question_answers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "book_bookmarks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Adopt all pre-multi-user rows: they belong to the seeded initial
            // account (see 20260603213712_AddUsers) — this must precede the FKs
            // below, which the all-zeros placeholder would violate. The DEFAULT
            // exists only to add a NOT NULL column to a non-empty table; drop it
            // so a missing user_id can never be silently masked at insert time.
            migrationBuilder.Sql(
                """
                UPDATE reading_progress SET user_id = '00000000-0000-0000-0000-000000000001';
                UPDATE book_bookmarks SET user_id = '00000000-0000-0000-0000-000000000001';
                UPDATE question_answers SET user_id = '00000000-0000-0000-0000-000000000001';
                ALTER TABLE reading_progress ALTER COLUMN user_id DROP DEFAULT;
                ALTER TABLE book_bookmarks ALTER COLUMN user_id DROP DEFAULT;
                ALTER TABLE question_answers ALTER COLUMN user_id DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_reading_progress_user_id",
                table: "reading_progress",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "reading_progress_book_user_device",
                table: "reading_progress",
                columns: new[] { "book_id", "user_id", "device_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "reading_progress_book_user_updated",
                table: "reading_progress",
                columns: new[] { "book_id", "user_id", "updated_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_question_answers_user_id",
                table: "question_answers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "book_bookmarks_book_user",
                table: "book_bookmarks",
                columns: new[] { "book_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_book_bookmarks_user_id",
                table: "book_bookmarks",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_book_bookmarks_users_user_id",
                table: "book_bookmarks",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_question_answers_users_user_id",
                table: "question_answers",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_reading_progress_users_user_id",
                table: "reading_progress",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_book_bookmarks_users_user_id",
                table: "book_bookmarks");

            migrationBuilder.DropForeignKey(
                name: "FK_question_answers_users_user_id",
                table: "question_answers");

            migrationBuilder.DropForeignKey(
                name: "FK_reading_progress_users_user_id",
                table: "reading_progress");

            migrationBuilder.DropIndex(
                name: "IX_reading_progress_user_id",
                table: "reading_progress");

            migrationBuilder.DropIndex(
                name: "reading_progress_book_user_device",
                table: "reading_progress");

            migrationBuilder.DropIndex(
                name: "reading_progress_book_user_updated",
                table: "reading_progress");

            migrationBuilder.DropIndex(
                name: "IX_question_answers_user_id",
                table: "question_answers");

            migrationBuilder.DropIndex(
                name: "book_bookmarks_book_user",
                table: "book_bookmarks");

            migrationBuilder.DropIndex(
                name: "IX_book_bookmarks_user_id",
                table: "book_bookmarks");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "reading_progress");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "question_answers");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "book_bookmarks");

            // Multi-user rows can collide on (book_id, device_id) once user_id
            // is gone — two accounts reading the same book on a shared device.
            // Keep each pair's most recent row so recreating the old unique
            // index can't abort the rollback partway.
            migrationBuilder.Sql(
                """
                DELETE FROM reading_progress p
                USING reading_progress q
                WHERE p.book_id = q.book_id AND p.device_id = q.device_id
                  AND (p.updated_at < q.updated_at OR (p.updated_at = q.updated_at AND p.id < q.id));
                """);

            migrationBuilder.CreateIndex(
                name: "reading_progress_book_device",
                table: "reading_progress",
                columns: new[] { "book_id", "device_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "reading_progress_book_updated",
                table: "reading_progress",
                columns: new[] { "book_id", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "book_bookmarks_book",
                table: "book_bookmarks",
                column: "book_id");
        }
    }
}

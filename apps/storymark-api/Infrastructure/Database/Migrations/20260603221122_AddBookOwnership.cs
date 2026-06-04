using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBookOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "books_content_hash",
                table: "books");

            migrationBuilder.AddColumn<bool>(
                name: "is_public",
                table: "books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "books",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Adopt all pre-multi-user books: they belong to the seeded initial
            // account (see 20260603213712_AddUsers) — this must precede the FK
            // below, which the all-zeros placeholder would violate. The DEFAULT
            // exists only to add a NOT NULL column to a non-empty table; drop it
            // so a missing owner can never be silently masked at insert time.
            migrationBuilder.Sql(
                """
                UPDATE books SET owner_user_id = '00000000-0000-0000-0000-000000000001';
                ALTER TABLE books ALTER COLUMN owner_user_id DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "books_content_hash_owner",
                table: "books",
                columns: new[] { "content_hash", "owner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_books_owner_user_id",
                table: "books",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "books_public",
                table: "books",
                column: "is_public",
                filter: "is_public");

            migrationBuilder.AddForeignKey(
                name: "FK_books_users_owner_user_id",
                table: "books",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_books_users_owner_user_id",
                table: "books");

            migrationBuilder.DropIndex(
                name: "books_content_hash_owner",
                table: "books");

            migrationBuilder.DropIndex(
                name: "IX_books_owner_user_id",
                table: "books");

            migrationBuilder.DropIndex(
                name: "books_public",
                table: "books");

            migrationBuilder.DropColumn(
                name: "is_public",
                table: "books");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "books");

            migrationBuilder.CreateIndex(
                name: "books_content_hash",
                table: "books",
                column: "content_hash",
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    normalized_user_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_normalized_user_name",
                table: "users",
                column: "normalized_user_name",
                unique: true);

            // Seed the initial account so existing single-user data has an owner
            // for later migrations to adopt (S7.2 backfills against this
            // well-known id). Passwordless = cannot log in until the operator
            // sets a password via the admin API (ADR-0023). The stamps are fixed
            // literals: migrations must be deterministic, and the security stamp
            // rotates on first password set anyway.
            migrationBuilder.Sql(
                """
                INSERT INTO users (id, user_name, normalized_user_name, password_hash, security_stamp, concurrency_stamp, lockout_end, lockout_enabled, access_failed_count, created_at)
                VALUES ('00000000-0000-0000-0000-000000000001', 'reader', 'READER', NULL, 'SEEDSECURITYSTAMP0000000000000001', 'f3b9a4c2-7d15-4e8a-9b06-2c5d8e1f4a73', NULL, TRUE, 0, now());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIsAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The seeded account (AddUsers) becomes the first admin, so an
            // existing install's operator keeps admin access across the switch
            // from the shared bearer token to per-user admin (ADR-0024). On a
            // fresh database the account is still passwordless; see
            // AdminBootstrap for how it gets its first password.
            migrationBuilder.Sql(
                """
                UPDATE users SET is_admin = TRUE WHERE id = '00000000-0000-0000-0000-000000000001';
                """);

            // The shared operator token is gone (ADR-0024); drop its settings
            // row so the masked secret no longer appears in the admin UI.
            migrationBuilder.Sql(
                """
                DELETE FROM settings WHERE key = 'auth.bearerToken';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_admin",
                table: "users");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    value_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.key);
                });

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION settings_notify() RETURNS trigger AS $$
                BEGIN
                  PERFORM pg_notify('settings_changed', COALESCE(NEW.key, OLD.key));
                  RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER settings_notify_trigger
                AFTER INSERT OR UPDATE OR DELETE ON settings
                FOR EACH ROW EXECUTE FUNCTION settings_notify();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS settings_notify_trigger ON settings;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS settings_notify();");

            migrationBuilder.DropTable(
                name: "settings");
        }
    }
}

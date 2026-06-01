using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmCallStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "error",
                table: "llm_calls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "succeeded",
                table: "llm_calls",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "error",
                table: "llm_calls");

            migrationBuilder.DropColumn(
                name: "succeeded",
                table: "llm_calls");
        }
    }
}

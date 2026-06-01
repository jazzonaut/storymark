using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBookProcessingError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "processing_error",
                table: "books",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "processing_error",
                table: "books");
        }
    }
}

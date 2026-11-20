using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddModelReasoningAndLuna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reasoning_effort",
                table: "llm_models",
                type: "text",
                nullable: true);

            // Keep registry IDs so every existing task assignment follows the
            // replacement. Scope this one-time upgrade to OpenRouter; local
            // models and other providers retain their existing configuration.
            migrationBuilder.Sql(
                """
                UPDATE llm_models AS m
                SET model_name = 'openai/gpt-6-luna',
                    label = CASE WHEN lower(m.label) IN ('gpt-4o mini', 'gpt-4o-mini')
                        THEN 'GPT-6 Luna' ELSE m.label END,
                    reasoning_effort = 'none',
                    input_per_million = 0.10,
                    output_per_million = 0.50,
                    updated_at = CURRENT_TIMESTAMP
                FROM llm_providers AS p
                WHERE m.provider_id = p.id
                    AND p.kind = 'OpenAiCompatible'
                    AND p.base_url ~* '^https?://([a-z0-9-]+[.])*openrouter[.]ai(:[0-9]+)?(/|$)'
                    AND m.model_name IN ('openai/gpt-4o-mini', 'openai/gpt-6-luna');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Model selection is operator configuration: rolling back the schema
            // must not revert models the operator may have edited since upgrading.
            migrationBuilder.DropColumn(
                name: "reasoning_effort",
                table: "llm_models");
        }
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Storymark.Api.Infrastructure.Database.Migrations;

/// <inheritdoc />
[DbContext(typeof(StorymarkDbContext))]
[Migration("20261120210000_DefaultGeminiChatRoutes")]
public sealed class DefaultGeminiChatRoutes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $upgrade$
            DECLARE
                chat_keys text[] := ARRAY[
                    'llm.chat.taskMap.qa', 'llm.chat.taskMap.extract', 'llm.chat.taskMap.entity_summary',
                    'llm.chat.taskMap.selection_hint', 'llm.chat.taskMap.memory', 'llm.chat.taskMap.current_scene'
                ];
                account uuid;
                gemini uuid;
            BEGIN
                -- Reuse the active Luna account, preferring the account used for extraction.
                SELECT p.id INTO account FROM llm_providers p
                JOIN llm_models m ON m.provider_id = p.id
                JOIN settings s ON s.value = to_jsonb(m.id::text)
                WHERE p.kind = 'OpenAiCompatible'
                  AND p.base_url ~* '^https?://([a-z0-9-]+[.])*openrouter[.]ai(:[0-9]+)?(/|$)'
                  AND m.model_name = 'openai/gpt-6-luna' AND s.key = ANY(chat_keys)
                GROUP BY p.id ORDER BY bool_or(s.key = 'llm.chat.taskMap.extract') DESC, count(*) DESC, p.id
                LIMIT 1;
                IF account IS NULL THEN
                    RETURN;
                END IF;

                SELECT id INTO gemini FROM llm_models
                WHERE provider_id = account AND model_name = 'google/gemini-2.5-flash-lite'
                  AND reasoning_effort IS NULL
                ORDER BY created_at, id LIMIT 1;

                IF gemini IS NULL THEN
                    gemini := gen_random_uuid();
                    INSERT INTO llm_models (id, provider_id, label, model_name, input_per_million,
                                            output_per_million, created_at, updated_at)
                    VALUES (gemini, account, 'Gemini 2.5 Flash-Lite', 'google/gemini-2.5-flash-lite',
                            0.10, 0.40, now(), now());
                END IF;

                INSERT INTO settings (key, value, value_type, is_secret, updated_at, updated_by)
                SELECT k, to_jsonb(gemini::text), 'String', false, now(), 'seed' FROM unnest(chat_keys) AS k
                ON CONFLICT (key) DO UPDATE
                SET value = excluded.value, updated_at = excluded.updated_at, updated_by = excluded.updated_by;
            END $upgrade$;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Routing can be edited after upgrade. Preserve the operator's selections.
    }
}

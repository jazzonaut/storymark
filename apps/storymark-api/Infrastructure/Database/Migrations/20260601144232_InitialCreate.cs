using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Storymark.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "books",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "text", nullable: true),
                    language = table.Column<string>(type: "text", nullable: true),
                    format = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    cover_path = table.Column<string>(type: "text", nullable: true),
                    processing_status = table.Column<string>(type: "text", nullable: false),
                    processing_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_books", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "llm_calls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    response = table.Column<string>(type: "text", nullable: true),
                    tokens_in = table.Column<int>(type: "integer", nullable: true),
                    tokens_out = table.Column<int>(type: "integer", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    cost_estimate = table.Column<decimal>(type: "numeric", nullable: true),
                    book_id = table.Column<Guid>(type: "uuid", nullable: true),
                    section_index = table.Column<int>(type: "integer", nullable: true),
                    used_future_content = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_calls", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "llm_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: false),
                    api_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_providers", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "book_bookmarks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    paragraph_index = table.Column<int>(type: "integer", nullable: true),
                    location_start = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_bookmarks", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_bookmarks_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_entities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canonical_name = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    aliases = table.Column<string>(type: "jsonb", nullable: false),
                    first_seen_section = table.Column<int>(type: "integer", nullable: true),
                    first_seen_location = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_entities", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_entities_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    location_start = table.Column<int>(type: "integer", nullable: false),
                    location_end = table.Column<int>(type: "integer", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    involved_entity_ids = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_events_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_memory_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    up_to_section = table.Column<int>(type: "integer", nullable: false),
                    up_to_location = table.Column<int>(type: "integer", nullable: false),
                    summary_so_far = table.Column<string>(type: "text", nullable: false),
                    timeline = table.Column<string>(type: "jsonb", nullable: false),
                    open_threads = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_memory_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_memory_snapshots_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    source_href = table.Column<string>(type: "text", nullable: true),
                    html_path = table.Column<string>(type: "text", nullable: true),
                    text_path = table.Column<string>(type: "text", nullable: true),
                    text_hash = table.Column<string>(type: "text", nullable: true),
                    word_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_sections", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_sections_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "reading_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "text", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    paragraph_index = table.Column<int>(type: "integer", nullable: true),
                    location_start = table.Column<int>(type: "integer", nullable: false),
                    location_end = table.Column<int>(type: "integer", nullable: false),
                    client_timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reading_progress", x => x.id);
                    table.ForeignKey(
                        name: "FK_reading_progress_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "section_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    important_events = table.Column<string>(type: "jsonb", nullable: false),
                    entities_seen = table.Column<string>(type: "jsonb", nullable: false),
                    open_threads = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_section_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_section_summaries_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "llm_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_name = table.Column<string>(type: "text", nullable: false),
                    input_per_million = table.Column<decimal>(type: "numeric", nullable: true),
                    output_per_million = table.Column<decimal>(type: "numeric", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_models", x => x.id);
                    table.ForeignKey(
                        name: "FK_llm_models_llm_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "llm_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "book_entity_mentions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    location_start = table.Column<int>(type: "integer", nullable: false),
                    location_end = table.Column<int>(type: "integer", nullable: false),
                    mention_text = table.Column<string>(type: "text", nullable: false),
                    surrounding_text = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_entity_mentions", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_entity_mentions_book_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "book_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_entity_mentions_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_entity_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    up_to_section = table.Column<int>(type: "integer", nullable: false),
                    up_to_location = table.Column<int>(type: "integer", nullable: false),
                    short_summary = table.Column<string>(type: "text", nullable: false),
                    known_facts = table.Column<string>(type: "jsonb", nullable: false),
                    relationships = table.Column<string>(type: "jsonb", nullable: false),
                    open_questions = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_entity_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_entity_summaries_book_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "book_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_entity_summaries_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_relationships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship_type = table.Column<string>(type: "text", nullable: false),
                    polarity = table.Column<string>(type: "text", nullable: true),
                    evidence = table.Column<string>(type: "text", nullable: true),
                    first_seen_section = table.Column<int>(type: "integer", nullable: false),
                    first_seen_location = table.Column<int>(type: "integer", nullable: false),
                    last_seen_section = table.Column<int>(type: "integer", nullable: true),
                    last_seen_location = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_relationships", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_relationships_book_entities_from_entity_id",
                        column: x => x.from_entity_id,
                        principalTable: "book_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_relationships_book_entities_to_entity_id",
                        column: x => x.to_entity_id,
                        principalTable: "book_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_relationships_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    location_start = table.Column<int>(type: "integer", nullable: false),
                    location_end = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    token_estimate = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector", nullable: true),
                    embedding_model_id = table.Column<string>(type: "text", nullable: true),
                    embedding_model_version = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_chunks_book_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "book_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_chunks_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_paragraphs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_index = table.Column<int>(type: "integer", nullable: false),
                    paragraph_index = table.Column<int>(type: "integer", nullable: false),
                    location_start = table.Column<int>(type: "integer", nullable: false),
                    location_end = table.Column<int>(type: "integer", nullable: false),
                    html = table.Column<string>(type: "text", nullable: true),
                    plain_text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_paragraphs", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_paragraphs_book_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "book_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "book_bookmarks_book",
                table: "book_bookmarks",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "book_chunks_book_section_chunk",
                table: "book_chunks",
                columns: new[] { "book_id", "section_index", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "book_chunks_pos",
                table: "book_chunks",
                columns: new[] { "book_id", "section_index", "location_start" });

            migrationBuilder.CreateIndex(
                name: "IX_book_chunks_section_id",
                table: "book_chunks",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "book_entities_book_canonical",
                table: "book_entities",
                columns: new[] { "book_id", "canonical_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "book_entity_mentions_entity_pos",
                table: "book_entity_mentions",
                columns: new[] { "book_id", "entity_id", "section_index" });

            migrationBuilder.CreateIndex(
                name: "IX_book_entity_mentions_entity_id",
                table: "book_entity_mentions",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "book_entity_summaries_pos",
                table: "book_entity_summaries",
                columns: new[] { "entity_id", "up_to_section", "up_to_location" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_book_entity_summaries_book_id",
                table: "book_entity_summaries",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "book_events_pos",
                table: "book_events",
                columns: new[] { "book_id", "section_index", "location_start" });

            migrationBuilder.CreateIndex(
                name: "book_memory_snapshots_pos",
                table: "book_memory_snapshots",
                columns: new[] { "book_id", "up_to_section", "up_to_location" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "book_paragraphs_book_section_index",
                table: "book_paragraphs",
                columns: new[] { "book_id", "section_index", "paragraph_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_book_paragraphs_section_id",
                table: "book_paragraphs",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "book_relationships_pos",
                table: "book_relationships",
                columns: new[] { "book_id", "first_seen_section" });

            migrationBuilder.CreateIndex(
                name: "IX_book_relationships_from_entity_id",
                table: "book_relationships",
                column: "from_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_book_relationships_to_entity_id",
                table: "book_relationships",
                column: "to_entity_id");

            migrationBuilder.CreateIndex(
                name: "book_sections_book_index",
                table: "book_sections",
                columns: new[] { "book_id", "section_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "books_content_hash",
                table: "books",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "llm_calls_task_time",
                table: "llm_calls",
                columns: new[] { "task", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "llm_models_provider",
                table: "llm_models",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "llm_providers_name",
                table: "llm_providers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "question_answers_book_time",
                table: "question_answers",
                columns: new[] { "book_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "reading_progress_book_device",
                table: "reading_progress",
                columns: new[] { "book_id", "device_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "section_summaries_book_section",
                table: "section_summaries",
                columns: new[] { "book_id", "section_index" },
                unique: true);

            // GIN full-text index over the chunk text (docs/plan.md §13). This is an
            // expression index on the immutable two-arg to_tsvector, which EF can't
            // model via the fluent API, so it's created in raw SQL and is invisible to
            // the model snapshot (EF won't try to manage or drop it).
            migrationBuilder.Sql(
                "CREATE INDEX book_chunks_fts ON book_chunks USING gin (to_tsvector('english', text));");

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
            migrationBuilder.Sql("DROP INDEX IF EXISTS book_chunks_fts;");

            migrationBuilder.DropTable(
                name: "book_bookmarks");

            migrationBuilder.DropTable(
                name: "book_chunks");

            migrationBuilder.DropTable(
                name: "book_entity_mentions");

            migrationBuilder.DropTable(
                name: "book_entity_summaries");

            migrationBuilder.DropTable(
                name: "book_events");

            migrationBuilder.DropTable(
                name: "book_memory_snapshots");

            migrationBuilder.DropTable(
                name: "book_paragraphs");

            migrationBuilder.DropTable(
                name: "book_relationships");

            migrationBuilder.DropTable(
                name: "llm_calls");

            migrationBuilder.DropTable(
                name: "llm_models");

            migrationBuilder.DropTable(
                name: "question_answers");

            migrationBuilder.DropTable(
                name: "reading_progress");

            migrationBuilder.DropTable(
                name: "section_summaries");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "book_sections");

            migrationBuilder.DropTable(
                name: "book_entities");

            migrationBuilder.DropTable(
                name: "llm_providers");

            migrationBuilder.DropTable(
                name: "books");
        }
    }
}

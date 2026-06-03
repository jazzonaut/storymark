# `apps/storymark-api`

ASP.NET Core API for Storymark. It serves the reader API, admin API, Hangfire jobs, pgvector search, LLM routing, and the built Vue SPA.

## Current Surface

- Public reader/library endpoints for upload, book list, covers/resources, sections, progress, bookmarks, Q&A, selection hints, memory, entities, events, and relationships.
- Admin endpoints for settings, prompts, providers/models, LLM call log, cost summary, embedding swaps, section-title backfill, stuck-upload recovery, and explicit book reprocessing.
- Background pipeline: EPUB parse -> chunk -> per-section extraction -> rolling memory snapshots -> embeddings -> `Indexed`.
- LLM abstraction for Ollama and OpenAI-compatible providers, with per-task model routing and audited call logging.
- EF Core/Postgres storage with pgvector, Hangfire on Postgres, and filesystem book artifacts under `/data/books/{sha256}`.

## Layout

```text
Domain/             persisted entities and small domain enums
Endpoints/          FastEndpoints grouped by public/admin/debug route surface
Infrastructure/     database config, auth, LLM clients, settings, storage
Jobs/               Hangfire pipeline and maintenance jobs
Prompts/            first-run prompt defaults and placeholder contracts
Services/           book, retrieval, assistant, entity, memory, settings, LLM logic
```

## Run

From `apps/storymark-api`:

```bash
dotnet run
```

From the repo root:

```bash
dotnet build apps/storymark-api -c Release -warnaserror
dotnet format apps/storymark-api --verify-no-changes
```

Local launch settings serve the API on `http://localhost:8088` for the Vite proxy. In production, the API also serves the built SPA from `wwwroot`.

# 0004 — Hangfire on Postgres for background jobs

**Status:** accepted

## Context

Storymark has a non-trivial set of background jobs: EPUB parsing, chunking, embedding, per-section LLM extraction, memory-snapshot updates, lazy entity-summary generation, embedding-model reindexes. Each job is slow (LLM bound) and must survive container restarts. We also need a queue dashboard for debugging stuck or failed jobs while iterating on prompts.

A hand-rolled `BackgroundService + jobs table` could work, but reimplements retries, dead-letter, priority, and observability — multiple weeks of work for parity with mature options.

## Decision

Use **Hangfire** with the Postgres storage adapter. The Postgres database is already in the stack; Hangfire's dashboard is mounted at `/hangfire` and gated by the admin bearer token.

Jobs use Hangfire priority queues: on-demand work (`EntitySummaryJob` triggered by a user tap) jumps over background work (`ProcessBookJob`, `EmbeddingJob`, `SectionExtractionJob`).

## Consequences

- Persistent jobs survive restarts.
- Free dashboard for debugging without extra UI work.
- Built-in retries with exponential backoff.
- One more library to keep current; pinned exactly in `Directory.Packages.props`.
- Storage tables live alongside app data — backed up together.

## Alternatives considered

- **Custom `BackgroundService` + `jobs` table** — full control, but weeks of polish to match Hangfire's defaults; rebuilding the dashboard alone is more work than the rest of admin combined.
- **Coravel** — lightweight, but in-memory by default; a container restart loses the queue.
- **In-process `Channel<T>` queue** — fine for prototype, but zero observability and no persistence. Would have been replaced anyway.
- **Quartz.NET** — better for cron-style scheduling, less ergonomic for ad-hoc work queues; revisit if we need persistent scheduled tasks beyond Hangfire's own recurring-job support.

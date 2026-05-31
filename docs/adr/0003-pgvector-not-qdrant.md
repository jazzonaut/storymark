# 0003 — pgvector inside Postgres, not a separate Qdrant service

**Status:** accepted

## Context

The original plan called for Qdrant as a dedicated vector store alongside Postgres. Qdrant is genuinely excellent at scale and has a clean payload-filtering API, but Storymark indexes personal book libraries — a handful of books, a few thousand chunks each, never millions of vectors. The deployment footprint and the cognitive cost of running two databases are noticeable for this scale.

Storymark's spoiler-safe filter (`section_index <= currentSection AND ...`) is a positional predicate, which means every retrieval is a hybrid of semantic similarity + structured filtering + (post-MVP) full-text. Doing this across two stores requires either client-side joining or a coordinator service — work that disappears entirely when everything is in one database.

## Decision

Use **pgvector** inside Postgres. The `book_chunks` table has an `embedding vector(768)` column with an HNSW index; full-text uses Postgres FTS on the same row. Hybrid retrieval becomes a single SQL statement that takes the spoiler filter as a `WHERE` clause.

## Consequences

- One database to back up, secure, monitor.
- Hybrid retrieval is one query with `JOIN`s and `WHERE`, not a coordinator.
- pgvector HNSW handles our dataset comfortably on the reference homelab.
- We give up Qdrant's richer payload filter DSL, but our filter is structurally simple.
- Future scale-out (millions of vectors) would require revisiting. The `SpoilerSafeRetrievalService` abstraction means that's a contained migration if it ever happens.

## Alternatives considered

- **Qdrant** — better at >10M vectors and richer filter API; both irrelevant at our scale.
- **Abstract `IVectorStore` interface, ship pgvector first** — an abstraction we don't currently need. The right time to add it is when a second backend is real.
- **SQLite + sqlite-vec** — viable for embedded use, but we already need Postgres for app data, so adding SQLite for vectors only would be redundant.

# Delivery Plan

A one-page view of the road from empty repo to a daily-driver reader with memory. The detailed plan is in [`plan.md`](plan.md); the issue-level breakdown is in [`issues.md`](issues.md).

## Phase 0 — Project setup

- Monorepo scaffold; frontend boots; backend health endpoint; Compose stack lights up.
- Outputs: `pnpm dev` works, `task ci` is green, `docker compose up` brings all three services healthy.

## Phase 0.5 — Admin shell + settings table

- DB-backed configuration with hot reload, `IChatModelClient` abstraction, LLM call log, admin UI for settings + prompts + call log + cost meter.
- Outputs: every later phase reads its config from the DB. No restart needed to swap models or edit prompts.

## Phase 1 — Book upload and library

- Upload EPUB → SHA-256 dedup → file storage → library card with cover and processing status.
- Outputs: I can see my books.

## Phase 2 — Paginated reader (Readera replacement)

- EPUB parsing, sanitized section HTML, paginated CSS-columns reader, typography controls, bookmarks, whole-book offline cache, backend-authoritative LWW progress sync.
- Outputs: I can read offline on my phone, save my place across devices, bookmark passages. **Storymark already replaces Kindle/Readera for raw reading at this point.**

## Phase 3 — Chunking + indexing + hybrid search

- Paragraph-aware chunking, pgvector + HNSW + FTS, unified hybrid retrieval, spoiler-safe filter, embedding-model swap flow.
- Outputs: the data substrate for every AI feature is in place. No user-visible AI features yet.

## Phase 4 — Spoiler-safe Q&A + selection hint

- `/ask` endpoint with hybrid retrieval and sources, assistant panel in the reader, tiered selection popover (entity hit → 1B hint → escalate).
- Outputs: I can ask the book questions about what I've read. Selection feels alive.

## Phase 5 — Section extraction + memory + recap card

- Single-pass per-section extraction, rolling memory snapshots, dismissible section recap card, Previously card on long reopens.
- Outputs: every chapter boundary surfaces a recap. The book remembers itself for me.

## Phase 6 — Story Guide, timeline, relationship graph

- Entity popups, Story Guide panel, ECharts timeline + force-directed relationship graph, fully filtered to current reading position.
- Outputs: the visual memory surfaces are live. The Memory panel is feature-complete for v1.

## Phase 7 onwards

Capacitor wrap-up; highlights & notes; format-handler expansion (TXT, PDF); admin-side entity curation; reverse-proxy/auth hardening; eventual reintroduction of automated tests ([`adr/0012-tests-deferred.md`](adr/0012-tests-deferred.md)).

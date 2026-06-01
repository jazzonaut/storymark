# Storymark — Phase 0–6 Backlog

Each item below is a **vertical slice**: an independently grabbable piece of work that, when merged, delivers something demonstrable end-to-end. Pure schema-only or pure-UI-with-mocks issues are avoided — every slice cuts through the stack as far as it needs to.

Issue IDs are stable. Dependencies are listed; everything else can be parallelised.

Estimates are rough T-shirt sizes: **S** ≈ ½ day, **M** ≈ 1–2 days, **L** ≈ 3–5 days, **XL** ≈ 1+ week.

---

## Phase 0 — Project Setup

### S0.1 — Monorepo scaffold + frontend boots — **M**
**Goal:** `pnpm dev` opens a Vue 3 app with Tailwind + PrimeVue working; `pnpm lint` and `pnpm typecheck` pass.
**Backend:** none.
**Frontend:**
- pnpm workspace at repo root referencing `apps/storymark-app`, `packages/shared-types`
- pnpm catalog of exact-pinned versions (no `^`/`~`) — every workspace package uses `"vue": "catalog:"` etc.
- Vite + Vue 3 + TypeScript app
- TypeScript in **strict mode** (`"strict": true`, `"noUncheckedIndexedAccess": true`, `"noImplicitOverride": true`, `"exactOptionalPropertyTypes": true`)
- Tailwind configured with a base theme palette (light/sepia/dark tokens)
- PrimeVue installed and one demo `<Button>` rendered on `/admin` route to prove it works
- Vue Router with `/` (Library placeholder) and `/admin` (PrimeVue shell placeholder)
- **oxlint** for linting with strict ruleset (categories `correctness:error`, `suspicious:error`, plugins `typescript, unicorn, import, promise, vue`)
- **oxfmt** for formatting (no Prettier)
- `pnpm lint`, `pnpm typecheck`, `pnpm format:check`, `pnpm build` scripts at the root
**Acceptance:** `pnpm install && pnpm dev` shows the library placeholder; `/admin` shows a PrimeVue button; `pnpm lint`, `pnpm typecheck`, `pnpm format:check`, and `pnpm build` all pass cleanly.
**Depends on:** —

### S0.2 — Backend scaffold + health endpoint — **M**
**Goal:** `curl /api/health` returns OK from a containerised ASP.NET Core service.
**Backend:**
- `Storymark.Api` project (ASP.NET Core, .NET 8/9)
- FastEndpoints wired
- `HealthEndpoint` returning `{ "status": "ok", "version": "..." }`
- EF Core wired to Postgres connection string (no migrations yet)
- Dockerfile (multi-stage, non-root user)
- Serilog with console JSON sink
**Frontend:** none.
**Acceptance:** running locally via `dotnet run` and inside Docker both serve `/api/health`.
**Depends on:** —

### S0.3 — Docker Compose stack lights up — **S**
**Goal:** `docker compose up` brings up api + pgvector-postgres + ollama; api passes health.
**Backend:**
- `docker-compose.yml` with the three services (env, volumes, healthchecks)
- `pgvector/pgvector:pg16` image for Postgres
- One-time init script that runs `CREATE EXTENSION vector;`
**Frontend:** none.
**Acceptance:** `docker compose up` brings everything to healthy; api can `SELECT 1;` against postgres; `ollama list` works inside the ollama container.
**Depends on:** S0.2

---

## Phase 0.5 — Admin Shell + Settings Table

### S0.5.1 — Settings table + hot-reloaded SettingsService — **L**
**Goal:** Reading a setting from any service returns the current DB value; updates propagate within seconds without restart.
**Backend:**
- EF migration: `settings(key, value jsonb, value_type, is_secret, updated_at, updated_by)`
- `SettingsService` with in-memory cache and `LISTEN/NOTIFY 'settings_changed'` subscriber
- AES-GCM encryption for `is_secret=true` values (key from `Settings__EncryptionKey` env var)
- Default-seeder on startup that inserts any missing keys from `appsettings.json` (`prompts.*`, `llm.chat.taskMap.*`, `llm.embedding.modelId`, `upload.maxBytes`, `auth.bearerToken`, `features.recapCardOnSectionEntry`)
- A throwaway internal endpoint `GET /api/_debug/setting?key=...` (guarded by env flag) for verifying the roundtrip
**Frontend:** none yet.
**Acceptance:** update a row in `settings` via `psql`, the debug endpoint reflects the change within 2s; encrypted values stay encrypted at rest.
**Depends on:** S0.3

### S0.5.2 — IChatModelClient + Ollama/OpenAI adapters + LlmCallRecorder — **L**
**Goal:** Any service can call `chatClient.CompleteAsync(task: "qa", ...)` and the call lands in `llm_calls`.
**Backend:**
- `IChatModelClient` interface
- `OllamaClient` implementation (chat + embed via HTTP)
- `OpenAiCompatibleClient` implementation (chat + embed; supports custom base URLs)
- `ChatModelRouter` that resolves `task → provider:model` from `SettingsService`
- EF migration: `llm_calls(...)` with `task_time` index
- `LlmCallRecorder` middleware/decorator that wraps every call: persist prompt, response, tokens (where available), latency, cost estimate
- Cost-rate config in settings (`llm.costRates.openai.gpt-4o` etc), used by recorder
- Internal debug endpoint `POST /api/_debug/chat?task=qa` returning the response (for manual verification)
**Frontend:** none.
**Acceptance:** call the debug endpoint with Ollama running → response returned, exactly one row added to `llm_calls` with non-null latency/tokens; switching `llm.chat.taskMap.qa` to a different Ollama model is reflected on the next call without restart.
**Depends on:** S0.5.1

### S0.5.3 — Bearer-token auth + admin endpoints — **M**
**Goal:** `/api/admin/*` is locked behind the configured bearer token; settings and prompts can be read and edited via API.
**Backend:**
- `BearerTokenAuthFilter` reading the active token from `settings.auth.bearerToken`
- Endpoints:
  - `GET /api/admin/settings` (list, secrets masked)
  - `PATCH /api/admin/settings` (`{ key, value, type }`)
  - `GET /api/admin/prompts` (returns `prompts.*` keys' current text)
  - `PATCH /api/admin/prompts/{key}` (`{ text }`)
  - `GET /api/admin/llm-calls?task=&limit=`
- Audit: every PATCH writes `updated_by = bearer-token-fingerprint`
**Frontend:** none.
**Acceptance:** unauthenticated request → 401; authenticated PATCH on a prompt key updates the value and the next chat call uses the new prompt.
**Depends on:** S0.5.1, S0.5.2

### S0.5.4 — Admin shell UI: settings + prompts editor — **L**
**Goal:** In the browser you can change LLM models and prompts and watch the change apply on the next call.
**Frontend:**
- Admin layout shell at `/admin` (PrimeVue `Menubar` + sidebar)
- "Settings" view: PrimeVue `DataTable` grouped by namespace, inline edit, secrets shown as `••••` with reveal-on-click
- "Prompts" view: list + Monaco-like textarea (CodeMirror 6) with save/revert
- Bearer-token entry on first load, stored in `localStorage`
- Toast on save success/failure
- `useAdmin.ts` composable wrapping `adminApi`
**Acceptance:** edit `llm.chat.taskMap.qa` and `prompts.qa` from the UI; trigger a chat call from the debug endpoint; new model and new prompt are used.
**Depends on:** S0.5.3, S0.1

### S0.5.5 — LLM call log + cost meter views — **M**
**Goal:** Every chat/embed call is visible in the admin UI with prompt, response, latency, tokens, cost; aggregate cost per task/book is shown.
**Frontend:**
- "LLM Calls" view: paginated table (task, provider:model, latency, tokens, cost, time); row expands to show prompt + response
- Filter chips by task, by book, by used_future_content
- "Cost Meter" view: bar chart by task (last 30 days), totals by book
**Backend:** add aggregation endpoint `GET /api/admin/llm-calls/cost-summary`.
**Acceptance:** run a few chat calls; they appear in the table within seconds; cost view shows non-zero totals when hosted rates are configured.
**Depends on:** S0.5.4

---

## Phase 1 — Book Upload and Library

### S1.1 — Upload an EPUB, see it in the library list — **L**
**Goal:** I can drag an EPUB into the upload screen, it lands on disk by SHA-256, dedup works, the library lists it.
**Backend:**
- EF migration: `books(...)`
- `IBookImportHandler` interface, `EpubImportHandler` stub (only metadata sniffing at this stage)
- `POST /api/books` multipart endpoint
  - validate `.epub` extension + magic bytes + size ≤ `upload.maxBytes`
  - SHA-256 hash, store at `/data/books/{hash}/original.epub`
  - dedup by hash; reuse existing book row on conflict
  - persist `books` row with `processing_status = Uploaded`
- `GET /api/books` returning the list
- `GET /api/books/{id}/cover` (returns placeholder image until Phase 2)
**Frontend:**
- `BookUploadView.vue` with drag-and-drop and file picker
- `LibraryView.vue` with `BookCard.vue` (cover, title, author placeholder, status badge)
- `useUpload.ts`, `useBooks.ts`
- Polling status badge for `Uploaded → Indexed`
**Acceptance:** upload a real EPUB; file lands on disk; library shows it; uploading the same file again reuses the existing row.
**Depends on:** S0.3

### S1.2 — Delete a book and wipe all its data — **S**
**Goal:** From the library I can delete a book; every trace — DB rows, on-disk files, and local caches — is removed, and re-uploading the same file starts fresh.
**Backend:**
- `FileStorageService.DeleteBookDirectory(hash)` — recursive delete of `{root}/books/{hash}/`, idempotent
- `BookDeletionService` — remove the `books` row (DB-level `ON DELETE CASCADE` clears all child tables), then delete the on-disk directory; DB-first so a file-IO failure orphans recoverable files rather than leaving a dangling library row
- `llm_calls` is preserved (nullable `book_id`, no FK) — it is the spoiler-safety audit trail + cost meter and outlives the book
- `DELETE /api/books/{id}` → 204, or 404 if unknown
**Frontend:**
- Overflow (⋯) menu on `BookCard.vue` with Delete; never triggers navigation
- `DeleteBookDialog.vue` — type-the-title-to-confirm modal (irreversible action)
- `purgeBookData(bookId)` clears every per-book Dexie store; `useBooks.remove()` calls the API, purges locally, then drops the list entry
**Acceptance:** delete a fully-processed book → it leaves the library, `{root}/books/{hash}/` is gone, all child rows are gone, `llm_calls` rows remain with their original `book_id`; re-uploading the same file recreates everything.
**Depends on:** S1.1

---

## Phase 2 — EPUB Parsing and Paginated Reading

### S2.1 — EPUB parsing produces readable sections — **XL**
**Goal:** A queued `ProcessBookJob` extracts metadata, cover, sections, paragraphs, and sanitized HTML. Book moves to `Parsed`.
**Backend:**
- Add Hangfire (Postgres storage), `/hangfire` dashboard gated by bearer token
- EF migrations: `book_sections`, `book_paragraphs`
- `EpubImportHandler` with VersOne.Epub: metadata, cover image extraction, reading order, per-section XHTML
- `ContentSanitizationService` using AngleSharp: strip scripts/handlers/external refs/inline styles to a known-good subset
- Plain-text extraction preserving paragraph offsets
- `ProcessBookJob`:
  - parse → store cover image → write section files at `/data/books/{hash}/sections/`
  - insert `book_sections` and `book_paragraphs` with offsets
  - update `processing_status` through `Parsing → Parsed`
- Enqueue job from `S1.1` upload handler
- Endpoints:
  - `GET /api/books/{id}/sections`
  - `GET /api/books/{id}/sections/{idx}` returning sanitized HTML + paragraph offsets
**Frontend:**
- Library card shows real cover once available
- Status badge reflects new statuses
**Acceptance:** upload an EPUB → background job runs → status reaches `Parsed`; section JSON includes correctly-offset paragraphs; sanitized HTML contains no `<script>`, no `on*` attributes, no `http://` resources.
**Depends on:** S1.1

### S2.2 — Paginated reader with typography controls — **L**
**Goal:** Tapping a book opens it in a Readera-style paginated reader; I can change font/size/theme and swipe between pages.
**Frontend:**
- `ReaderView.vue` route `/books/:id/read`
- `PaginatedSection.vue`: CSS multi-column layout (`column-width: 100vw`, `column-gap: 0`), horizontal scroll-snap, touch swipe + tap-edges to advance
- `ReaderToolbar.vue` (top, auto-hides after idle)
- Settings drawer: font family, size, line height, paragraph spacing, content width, theme (light/sepia/dark), entity highlight (off/subtle/always — wired in Phase 6)
- `useReader.ts` composable
- Pagination on resize and font change keeps the user on the same paragraph (compute via paragraph offsets)
**Acceptance:** open the EPUB from S2.1; swipe through pages; change font size → page reflows without losing position; theme switch is instant.
**Depends on:** S2.1

### S2.3 — PWA service worker + offline state — **M**
**Goal:** The reader works fully offline (airplane mode) and is installable as a PWA.

> **Scope note (S2.2):** The Dexie cache foundation moved into S2.2, which already ships: the `src/db/storymark-db.ts` schema (`manifests`, `sections`, `readerSettings`), the whole-book prefetch in parallel batches of 4 with a "Caching N/M" progress UI, and the read-through `readerApi` (IndexedDB-first, API fallback, write-back). S2.3 is the remaining PWA/offline layer on top of that.

**Frontend:**
- `useOfflineSync.ts` exposes online/offline state (Offline badge in library header + reader toolbar)
- Service worker (`vite-plugin-pwa`, `autoUpdate`): app-shell precache + `navigateFallback` for deep-link routes; runtime cache for covers + inline `resources/` images (CacheFirst) and `GET /api/books` (NetworkFirst, so the library renders offline). Section content stays Dexie-only (ADR-0006).
- "Installed app" check (BeforeInstallPromptEvent) and install button (library header)
- `bookmarks` / `pendingProgress` Dexie stores are added by S2.4/S2.5 when they have a consumer (not here)
**Acceptance:** open a book online → see caching progress → kill the dev server → reload PWA in airplane mode → can still read the whole book and swipe pages.
**Depends on:** S2.2

### S2.4 — Backend-authoritative progress with offline queue — **M**
**Goal:** Progress saves continuously; closing and reopening the book resumes at the same paragraph; offline writes flush on reconnect; last-write-wins across devices.
**Backend:**
- EF migration: `reading_progress(...)` with `client_timestamp`
- `POST /api/books/{id}/progress` accepting `{ sectionIndex, paragraphIndex, locationStart, locationEnd, clientTimestamp }`, upsert per `(book_id, device_id)`
- `GET /api/books/{id}/progress` returning the row with `MAX(client_timestamp)` across all devices
**Frontend:**
- Device id generated once and stored in `localStorage`
- Throttled progress writes (every 5s or on page change)
- Offline: write to Dexie `pendingProgress`, flush queue on reconnect
- Reader resumes at server position on open if server position is newer than local
**Acceptance:** read to paragraph N; close app; reopen → resumes at paragraph N. With phone offline, read to paragraph M; come online → server reflects paragraph M; a second device reading the same book picks up the latest paragraph.
**Depends on:** S2.2, S2.3

### S2.5 — Bookmarks — **M**
**Goal:** Tap the bookmark button on any page to save a position; bookmarks panel lists them grouped by chapter; tap to jump.
**Backend:**
- EF migration: `book_bookmarks(...)`
- `GET/POST/DELETE /api/books/{id}/bookmarks`
**Frontend:**
- `BookmarkButton.vue` in toolbar (toggle)
- `BookmarksPanel.vue` (bottom sheet) with chapter grouping
- Offline: same queue/replay pattern as progress
- Dexie store; sync on reconnect
**Acceptance:** bookmark a page; close and reopen; bookmark is there; tap → jumps to that paragraph; works offline and syncs.
**Depends on:** S2.4

---

## Phase 3 — Chunking, Indexing, Hybrid Search

### S3.1 — Paragraph-aware chunking — **M**
**Goal:** Every parsed section is chunked; chunks are queryable but not yet embedded.
**Backend:**
- EF migration: `book_chunks(...)` (with `embedding`, `embedding_model_id`, `embedding_model_version` columns, no HNSW index yet)
- `SectionChunkingService`: 500–900 words, 100–150 word overlap, paragraph-aware merging, preserve `(sectionIndex, location_start/end)`
- `ProcessBookJob` extended: after parsing, run chunking; status `Chunking → Indexed-without-vectors`
- Internal endpoint `GET /api/_debug/chunks/{bookId}` for verification
**Acceptance:** upload an EPUB; chunks for each section appear with sensible sizes and correct offsets.
**Depends on:** S2.1

### S3.2 — Embeddings + pgvector indexes — **L**
**Goal:** Chunks gain embeddings; HNSW + FTS indexes are live; status reaches `Indexed`.
**Backend:**
- HNSW index on `book_chunks.embedding`
- GIN FTS index on `to_tsvector('english', text)`
- `EmbeddingJob` (Hangfire, batch of 32) writes `embedding`, `embedding_model_id`, `embedding_model_version` using `ChatModelRouter.Embed(task: "embed")`
- Enqueued from `ProcessBookJob`; final status `Indexed`
- Internal debug endpoint `POST /api/_debug/search?bookId=&q=` returning top-K vector hits (no spoiler filter yet, gated by debug flag)
**Acceptance:** upload book → status reaches `Indexed`; debug search returns reasonable results.
**Depends on:** S3.1, S0.5.2

### S3.3 — Spoiler-safe hybrid retrieval helper — **L**
**Goal:** A single service method returns the top-K reranked, spoiler-filtered evidence chunks for a query string + position.
**Backend:**
- `SpoilerSafeRetrievalService` exposing `Search(bookId, queryText, currentSection, currentLocation, k)`
- Unified hybrid SQL (per §13 of the plan): semantic (vector) + keyword (FTS), filtered by spoiler position and current `embedding_model_id`
- Cross-encoder-free reranker: simple score normalisation + de-dup by `(sectionIndex, chunkIndex)`
- Internal debug endpoint `POST /api/_debug/search-safe` to inspect ranked hits
**Acceptance:** manual verification — debug call at `currentSection=3, currentLocation=500` never returns chunks from section 4+; chunks from section 3 with `locationStart > 500` are excluded; earlier chunks appear ranked.
**Depends on:** S3.2

### S3.4 — Embedding-model swap with warn-and-reindex — **M**
**Goal:** Admin can switch embedding model; UI warns about reindex, kicks off Hangfire jobs, old vectors are replaced.
**Backend:**
- `ReindexBookJob` (per book): re-embed all chunks under new `embedding_model_id`, delete rows tagged with old model id only after new ones land (per-chunk swap to avoid downtime)
- Endpoint `GET /api/admin/embedding/impact-preview` → `{ booksAffected, chunkCount, estimatedSeconds }`
- Endpoint `POST /api/admin/embedding/apply-swap` → enqueues per-book reindex jobs
**Frontend:**
- Settings editor intercepts changes to `llm.embedding.modelId` with a confirm dialog showing impact preview
- Progress shown in Job Dashboard (Hangfire UI link)
**Acceptance:** switch model from `nomic-embed-text` to a different embedding model → confirm dialog appears with correct counts → reindex completes → search uses the new model.
**Depends on:** S3.2, S0.5.4

---

## Phase 4 — Spoiler-Safe Q&A + Selection Hint

### S4.1 — `/ask` endpoint returns spoiler-safe answers with sources — **L**
**Goal:** Posting a question at a reading position returns an answer plus source snippets; never references future content; logged with `used_future_content=false`.
**Backend:**
- `AssistantService.AnswerAsync(bookId, question, currentSection, currentLocation)`
  - extract candidate entity terms from the question (capitalised n-grams + alias matches once Phase 6 lands; for now just capitalised n-grams)
  - small handcrafted synonym expansion (`jail/prison/...`, stored in `settings.search.synonyms`)
  - call `SpoilerSafeRetrievalService` for each query
  - merge + cap to top-12 evidence chunks
  - build prompt from `prompts.qa` + evidence + position
  - call `chatClient.CompleteAsync(task: "qa", ...)`
  - persist `question_answers` row + the `llm_calls` row carries `used_future_content` derived from evidence
- `POST /api/books/{id}/ask`
**Frontend:** none yet.
**Acceptance:** manual verification — given a sample book with known facts, asking a known-answer question returns the right snippet citations and `usedFutureContent: false`; asking a question whose answer is in a later chapter returns "That has not been revealed yet."
**Depends on:** S3.3

### S4.2 — Assistant panel UI — **M**
**Goal:** From the reader I can open an assistant panel, type a question, and see the streamed answer with tappable source chips that jump to the relevant paragraph.
**Frontend:**
- `MemoryPanel.vue` shell (bottom sheet on mobile, side panel on wide screens) with tabs (only "Assistant" wired in this issue)
- `QuestionInput.vue`, `AnswerCard.vue`, `SourceList.vue`
- `useAssistant.ts` calling `/ask` with current `(section, location)` from `useReader`
- Tap a source → reader navigates and highlights the paragraph for 2s
- Loading skeleton; error state when offline ("Q&A is unavailable offline")
**Acceptance:** ask a question from the reader; see answer + sources; tap a source → reader jumps; toggle offline → input shows the unavailable state.
**Depends on:** S4.1

### S4.3 — Selection hint endpoint (entity short-circuit + 1B fallback) — **M**
**Goal:** Selecting any passage returns either a cached entity card (instant) or a 1B-model hint (~1–3s); never reveals future content.
**Backend:**
- `SelectionHintService.GetHintAsync(bookId, selectedText, range, currentPosition)`:
  - normalize selection, attempt alias lookup in `book_entities` (Phase 6 will populate; for now match on exact canonical names from a stub table)
  - on hit → return `{ kind: "entity", entityId, card }` from cache
  - on miss → `SpoilerSafeRetrievalService.Search(selection, k=4)` then call `prompts.selectionHint` against `task: "selection_hint"`
- `POST /api/books/{id}/selection/hint`
**Frontend:** none.
**Acceptance:** manual verification — selection matching a known stub entity returns `kind=entity` with zero LLM calls; selection on novel text returns `kind=hint` with one `selection_hint` row in `llm_calls`.
**Depends on:** S3.3, S0.5.2

### S4.4 — Selection popover UI (tiered) — **M**
**Goal:** Long-press / mouseup in the reader shows a small popover; first state is chips; tapping a chip resolves to inline entity card or inline 1B hint with a "Tell me more" escalation.
**Frontend:**
- `SelectionPopover.vue`
- Selection capture: `selectionchange` event + position via `getBoundingClientRect()` of the range
- Chips: **Who/What is this? · Recap to here · Ask…**
- Resolved state renders `EntityCard.vue` or inline `HintText.vue`
- "Tell me more" → opens the assistant panel with the selection pre-filled
- Dismiss on tap-outside / scroll
**Acceptance:** select a passage on touch and mouse devices; popover behaves; entity hits are instant; novel selections show the hint within ~3s on local Ollama.
**Depends on:** S4.3, S4.2

---

## Phase 5 — Section Extraction, Memory, Recap Card

### S5.1 — SectionExtractionJob (combined extraction) — **L**
**Goal:** Each section gets a single LLM call that produces a summary, events, relationship deltas, and entities-introduced — all persisted.
**Backend:**
- EF migrations: `section_summaries`, `book_events`, `book_relationships`, `book_entities`, `book_entity_mentions`
- `SectionExtractionJob` (Hangfire, per section, normal priority):
  - load section text + rolling memory context for prompt
  - call `task: "extract"` with `prompts.sectionExtraction`
  - parse JSON; reconcile entities (canonical name match → reuse `book_entities` row; else insert)
  - resolve `fromName/toName` in `relationshipDeltas` to entity ids; if either side missing, create a `Unknown` entity
  - persist event rows with `involved_entity_ids`
  - tolerate malformed JSON: retry once with stricter prompt; on second failure log and continue
- Enqueued from `ProcessBookJob`; status sequence updated
- Endpoints (read):
  - `GET /api/books/{id}/events?upToSection=&upToLocation=`
  - `GET /api/books/{id}/relationships?upToSection=&upToLocation=`
**Acceptance:** uploading a small EPUB produces N section-summary rows, M event rows, K relationship rows; calling `/events?upToSection=3` and `/relationships?upToSection=3` returns nothing from sections 4+.
**Depends on:** S3.2, S0.5.2

### S5.2 — BookMemorySnapshotJob — **M**
**Goal:** A rolling "summary so far" exists at every section boundary, ready to render the recap card with zero LLM latency at read time.
**Backend:**
- EF migration: `book_memory_snapshots`
- `BookMemorySnapshotJob`:
  - input: previous snapshot (if any) + the new section summary
  - call `task: "memory"` with `prompts.bookMemory`
  - persist snapshot keyed on `(bookId, upToSection, upToLocation)`
- Enqueued by `SectionExtractionJob` on completion
- Endpoint: `GET /api/books/{id}/memory?upToSection=&upToLocation=` → latest snapshot at-or-before that position
**Acceptance:** after `SectionExtractionJob` for section 3 completes, a `book_memory_snapshots` row for section 3 exists and incorporates sections 1+2.
**Depends on:** S5.1

### S5.3 — Section recap card on section entry — **M**
**Goal:** Entering a new section auto-shows a dismissible recap card; respects the setting toggle; works offline if the snapshot is cached.
**Frontend:**
- `SectionRecapCard.vue` overlay anchored over the reader; dismiss on swipe-up / tap-X
- Trigger: `useReader` detects section-index change → fetch from Dexie first, else `/memory?upToSection=N-1`; show if data exists
- "Don't show again" link → updates `settings.features.recapCardOnSectionEntry`
- Dexie additions: cache fetched memory snapshots
**Acceptance:** read into chapter 4 → recap card slides in with content from chapters 1–3; dismiss → doesn't reappear until next section. Setting toggle disables it entirely.
**Depends on:** S5.2, S2.3

### S5.4 — Previously card on reopen >24h — **S**
**Goal:** Opening a book after >24h shows a "Previously…" card based on the latest snapshot at current position.
**Frontend:**
- `PreviouslyCard.vue` (same component family as `SectionRecapCard`)
- Trigger on book open: compare `lastOpenedAt` to now; if >24h, show
- Same data source (memory snapshot at-or-before current position)
**Acceptance:** open book, fast-forward `lastOpenedAt` in Dexie by 25h, reopen → card appears with correct content.
**Depends on:** S5.3

---

## Phase 6 — Story Guide, Timeline, Relationship Graph

### S6.1 — Entity popup + Story Guide panel — **L**
**Goal:** Tapping a known entity in the section opens a spoiler-safe popup with its short summary and a list of relationships; the Story Guide tab lists everything mentioned so far.
**Backend:**
- EF migration: `book_entity_summaries`
- `EntitySummaryJob` (Hangfire, priority high, on-demand):
  - inputs: `(bookId, entityId, currentSection, currentLocation)`
  - retrieve visible mentions + visible chunks for the entity
  - call `task: "entity_summary"` with `prompts.entitySummary`
  - cache row keyed on `(entityId, upToSection, upToLocation)`
- Endpoints:
  - `GET /api/books/{id}/entities?upToSection=&upToLocation=` — filtered list with short summary if cached
  - `GET /api/books/{id}/entities/{entityId}/summary?section=&location=` — returns cached summary, else enqueues `EntitySummaryJob` synchronously (await) and returns
  - `GET /api/books/{id}/sections/{idx}` upgraded to include `entities` array (visible only)
**Frontend:**
- `EntityHighlight.vue` wraps known canonical-name/alias hits in section HTML (subtle by default; respects highlight mode setting)
- `EntityPopup.vue` opens on entity tap
- `StoryGuidePanel.vue` (Memory tab): search + list, with `EntityCard.vue` and `EntityTypeBadge.vue`
- Selection-popover entity hit now uses the same endpoint
**Acceptance:** tap an entity in section 3 → popup shows summary using only sections 1–3 content; same entity tapped in section 7 → richer summary; Story Guide panel never lists entities not yet introduced.
**Depends on:** S5.1, S4.4

### S6.2 — Timeline chart — **L**
**Goal:** The Memory > Timeline tab shows a chronological view of events up to current position, rendered with ECharts; tap → jump to source.
**Frontend:**
- ECharts installed; lazy-loaded only for this tab
- `TimelineChart.vue`: horizontal time axis = section/location, points coloured by `eventType`, tooltip shows description + entities
- `EventDetailCard.vue` for selected event
- Tap an event → reader navigates to `(sectionIndex, locationStart)`
- `useTimeline.ts` fetches `/events?upToSection=&upToLocation=`; Dexie caches the response per-position with short TTL
**Backend:** endpoint already exists (S5.1); add an internal `_debug/seed-events` helper for manual exploration if needed.
**Acceptance:** open a processed book; timeline renders within 200ms for ≤100 events; events from future sections never appear; tap behaviour navigates correctly.
**Depends on:** S5.1, S5.3

### S6.3 — Relationship graph chart — **L**
**Goal:** The Memory > Graph tab renders an ECharts force-directed graph of entities and their relationships up to current position.
**Frontend:**
- `RelationshipGraphChart.vue` with force-directed layout
- Node = entity (size by mention count, colour by entity type)
- Edge = `book_relationships` row (colour by polarity, label by type, width by evidence count)
- Tap node → opens `EntityPopup`
- Pinch zoom + pan on mobile
- `useRelationshipGraph.ts` fetching `/relationships?upToSection=&upToLocation=`
- Dexie caches per-position with short TTL
**Backend:** endpoint already exists (S5.1).
**Acceptance:** graph renders on a book with ≥30 entities and ≥50 relationships; nodes draggable; tap behaviour opens entity popup; relationships beyond current position never appear.
**Depends on:** S5.1, S6.1

### S6.4 — Memory panel tabs wired end-to-end — **S**
**Goal:** The full Memory panel (Previously · Summary · Timeline · Graph · Story Guide · Assistant) is one cohesive surface.
**Frontend:**
- `MemoryPanel.vue` tabs polished: lazy-load each panel's data on first open
- Persist last-active tab per book in Dexie
- Empty states for each tab when data isn't ready ("Processing… check back in a minute")
- Keyboard shortcuts (desktop): `g m` to open, number keys to switch tabs
**Acceptance:** open the panel on a fully-processed book; every tab loads correctly; last tab is remembered across reopens.
**Depends on:** S4.2, S5.3, S6.1, S6.2, S6.3

---

## Cross-cutting (parallel to phases)

### CC.1 — CI: lint + typecheck + build + image push — **M**
**Goal:** CI (Forgejo Actions / GitHub Actions compatible) runs `pnpm lint`, `pnpm typecheck`, `pnpm build`, `dotnet format --verify-no-changes`, and `dotnet build -warnaserror` on every PR. On push to default branch it additionally builds and pushes a container image to the configured registry, then triggers a Portainer redeploy.
**Acceptance:** PR fails when lint/typecheck/build fail; push to default branch ends with a new image pushed and Portainer stack redeployed.
**Depends on:** S0.1, S0.2

### CC.2 — Backup script for `/data` + Postgres — **S**
**Goal:** A `backup.sh` script tars `/data` and `pg_dump`s the database to a configurable target directory (env-driven).
**Acceptance:** runs without errors; restoring from the produced archive recreates a working stack on a clean host.
**Depends on:** S0.3

### CC.3 — Spoiler-safety regression harness (deferred — Phase 7+) — **M**
**Goal (when adopted):** A reusable harness that, given a sample book + reading position, asserts no future content appears in any AI surface (Q&A, hint, entity, recap, timeline, graph). Until tests are reintroduced, this is enforced manually via the LLM call log and an admin-panel "future content" filter that surfaces any call with `used_future_content = true`.
**Depends on:** S0.5.5 (admin call log), eventual tests reintroduction (see [`adr/0012-tests-deferred.md`](adr/0012-tests-deferred.md)).

---

## Dependency graph (high level)

```text
S0.1 ─┐
S0.2 ─┼─ S0.3 ─ S0.5.1 ─ S0.5.2 ─ S0.5.3 ─ S0.5.4 ─ S0.5.5
      │                                                  
      └─ S1.1 ─ S2.1 ─ S2.2 ─ S2.3 ─ S2.4 ─ S2.5         
                       │                                  
                       └─ S3.1 ─ S3.2 ─ S3.3 ─ S3.4       
                                       │                  
                                       ├─ S4.1 ─ S4.2     
                                       └─ S4.3 ─ S4.4     
                                                  │       
                                       S5.1 ─ S5.2 ─ S5.3 ─ S5.4
                                       │                   
                                       └─ S6.1 ─ S6.2     
                                                └─ S6.3 ─ S6.4
```

## Suggested order of attack

1. **S0.1 + S0.2 + S0.3** in parallel (different layers, no overlap).
2. **S0.5.1 → S0.5.2 → S0.5.3 → S0.5.4** as a tight chain (admin foundations).
3. **S1.1** then **S2.1 → S2.2 → S2.3 → S2.4 → S2.5** — gives you a real Readera-replacement reader with offline + bookmarks.
4. **S0.5.5** drops in whenever the LLM call log feels useful (early; helps debug Phase 4+).
5. **S3.1 → S3.2 → S3.3** unlocks AI features.
6. **S4.1 → S4.2** for assistant; **S4.3 → S4.4** for selection.
7. **S5.1 → S5.2 → S5.3 → S5.4** for recap UX.
8. **S6.1 → (S6.2, S6.3)** parallel → **S6.4** wraps the Memory panel.

At any point after S4.2, you have a usable daily reader that already answers questions about what you've read. Everything after that is depth.

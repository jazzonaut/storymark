# Storymark — Current Context

A live pointer to where the project is right now. Keep it short. Move stable content into `docs/` and delete it from here.

Last updated: preparing the public GitHub release — README restructured (feature list, screenshots in `docs/assets/`, contributing section), CI `image` job now skips cleanly without registry secrets (and defaults to ghcr.io on github.com), PII audit of the tree and full git history came back clean.

## Status

**Phases 0–7 are complete and committed.** The slice-by-slice log that used to live here is preserved in this file's git history; `docs/issues.md` has the issue-level record. The short version:

- **Reader (Phases 0–2):** paginated offline-first PWA reader with typography/theme controls, whole-book offline download, backend-authoritative reading progress + bookmarks.
- **AI pipeline (Phases 3 + 5):** EPUB → sections → chunks → embeddings (pgvector); single-pass per-section extraction of summary/events/relationships/entities (ADR-0009/0018); rolling memory snapshots. Every LLM call flows through `SpoilerSafeRetrievalService` for evidence and `LlmCallRecorder` for audit — that pairing is the product invariant.
- **Memory surfaces (Phases 4 + 6):** assistant Q&A with jump-to-source citations, tiered selection popover with custom touch selection (ADR-0017), entity popup + Story Guide, ECharts timeline + relationship graph, scene card, section-entry recaps + the "Previously…" card — unified in the Memory panel.
- **Multi-user + auth (Phase 7, ADR-0023/0024/0025):** per-user accounts on an HttpOnly cookie session, per-user libraries/progress/bookmarks, book ownership + public/private visibility, per-account Dexie cache partitions, `is_admin` role gating `/admin` and Hangfire, internet-exposure hardening (forwarded headers, `Secure` cookies, login throttle + per-account lockout). First-boot admin password via `STORYMARK_BOOTSTRAP_PASSWORD`.
- **Ops + admin:** reference compose stack (standalone, caddy, and proxy-overlay variants), CI that validates both halves and pushes/deploys only when secrets are configured, deployment docs (`docs/deployment/`), spoiler-safety harness (`pnpm test:spoiler`), admin panel covering models/providers, prompts, settings, the LLM call log + cost meter, users, and book maintenance (stuck-book recovery, reprocess).

## In flight

**Public GitHub release.** Repo audited (no PII in tree or history), README rewritten with feature list + screenshots + contributing section, CI made fork-friendly (image/deploy jobs skip cleanly without secrets). Remaining: push to `github.com/jazzonaut/storymark`, set the repo description/topics, optionally add ghcr.io registry secrets.

Next up after that (see `docs/issues.md`, Cross-cutting):

- **CC.2** — backup script for `/data` + Postgres (`docs/deployment/05-backup-and-restore.md` documents the manual procedure; `scripts/` only has the spoiler harness so far).
- **CC.3** — golden fixtures + a CI policy for the spoiler-safety harness.
- **Tests** — ADR-0012's deferral window has closed (Phase 6 is done); broader automated coverage can start landing.
- **Capacitor wrap** (ADR-0005) when native niceties matter.

## Conventions snapshot

The full conventions live in `docs/conventions/`. The short version:

- Strict TypeScript; no `any`, no `as any`, no `!`.
- oxlint only; lint errors fail CI.
- Exact-pinned versions everywhere (pnpm catalog for JS, `Directory.Packages.props` for .NET).
- Tests were deferred through Phase 6 (`docs/adr/0012-tests-deferred.md`); that window has closed. The spoiler-safety harness (`pnpm test:spoiler`) is the first automated check; manual verification per issue still applies until broader coverage lands.
- Vertical slices only. PR body must include the Acceptance Criteria reproduction steps.
- Conventional Commits.

## Open questions

- **Q&A against a known-but-not-`Indexed` book retrieves no evidence (S4.1).** `/ask` 404s only for an *unknown* book; a book that exists but hasn't reached `Indexed` (no chunks/embeddings) retrieves nothing, so the model answers "That has not been revealed yet." — misleading for an unprocessed book. Not special-cased, consistent with the single-operator stance elsewhere; revisit with a "still processing" response if the assistant UI (S4.2) wants to distinguish the two.
- **`/ask` makes one `embed` `llm_calls` row per generated query (S4.1).** Up to 6 query embeds per question (original + entity n-grams + triggered synonym groups), on top of the chat call. Fine at single-operator scale and the embeds are book-attributed/auditable; if Q&A volume ever matters, dedupe/cache query embeds or batch them.
- **The local `qa` model under-answers from clearly-present context (S4.1).** `llama3.2:3b` sometimes prepends "That has not been revealed yet." and then states the correct fact, or hedges on an answerable question. The pipeline (retrieval, spoiler filter, sources, `used_future_content`) is correct; this is model/prompt quality. `prompts.qa` is operator-editable and a stronger `qa` model (or a hosted one via the OpenAI-compatible client) resolves it. Revisit prompt phrasing if a small model stays the target.
- **Prompts editor uses a plain `Textarea`, not CodeMirror 6 (S0.5.4 issue text).** CodeMirror would add several exact-pinned deps for what is plain text with `{placeholder}` tokens. Revisit if/when prompt editing wants placeholder highlighting or larger-document ergonomics.
- **Concurrent identical uploads aren't race-hardened (S1.1).** Two *simultaneous* uploads of the same never-seen file could both pass the dedup pre-check and then collide on either the `books_content_hash` unique index (`DbUpdateException`) or the `File.Move` into an existing dir (`IOException`), surfacing a 500 instead of a graceful dedup. Single-operator usage + the client double-submit guard make this unreachable in practice, so it's left uncoded per the "no fallbacks for cases that can't happen" rule; if multi-client upload ever lands, catch the unique-violation/`IOException` and re-read the existing row.
- **Library status poll runs while any book is non-terminal (S1.1).** `LibraryView` polls `GET /api/books` every 5s whenever `hasPending()` (any book not `Indexed`/`Failed`). S2.1 advances books to `Parsed`, which is still non-terminal, so a parsed-but-not-yet-indexed library keeps polling until the later embedding/extraction slices reach `Indexed`. Semantically correct (the book *is* mid-pipeline). Empty libraries don't poll.
- **Inline book images aren't guaranteed offline (S2.3).** Section *text* is fully offline (Dexie whole-book prefetch), but inline `resources/` images are only added to the `book-images` SW cache when a section that references them is actually rendered online (`CacheFirst` caches on first fetch). A book read for the first time offline shows broken images for sections not yet viewed online. Covers are cached the same way. The clean fix is to prefetch a section's `resources/` during `use-book-cache` (alongside the section HTML); deferred — text-first reading meets the S2.3 acceptance, and the target books' art is secondary. Revisit if image-heavy books matter.
- **`ProcessBookJob` paragraph extraction can drop the *direct* text of a mixed block (S2.1).** Paragraphs are the innermost elements of the block set; a block that has both direct text and a nested block (e.g. `<div>intro<ul>…</ul></div>` or `<li>lead-in <p>…</p></li>`) contributes only its nested blocks — the parent's own "intro"/"lead-in" text is not emitted as a paragraph and is absent from the plain-text/offset coordinate space (though still present in the section's full sanitised HTML). Rare in flat-`<p>` fiction (the target); the whole-body fallback only covers the *zero-blocks* case. A correct fix is a document-order DOM walk that interleaves a block's direct-text runs with its nested blocks — deferred; revisit if real books surface lost prose. **Reader impact (settled in S2.2):** the reader paginates **per-paragraph** (`paragraphs[].html`, for clean position-restore offsets), so this dropped lead-in text is currently invisible *in the reader* (it is still present in the section's full `html`). Confirmed with the S2.2 test EPUB (a `<div>lead-in<ul>…</ul></div>` block's lead-in is in `html` but not in any paragraph). Accepted for the flat-`<p>` fiction target; the eventual DOM-walk extraction fix resolves both the offset gap and the reader gap at once.
- **`ProcessBookJob` is not concurrency-hardened (S2.1).** It is idempotent for *sequential* re-runs (clears existing sections first), but two *simultaneous* runs for the same book (e.g. a manual Hangfire requeue while the first is still executing) could interleave delete+insert and collide on the `book_sections_book_index` unique constraint, surfacing as `Failed` with no auto-retry (`Attempts = 0`). Single-operator usage + enqueue-only-on-new-book make this unreachable in practice; left uncoded per the "no fallbacks for cases that can't happen" rule (same stance as the S1.1 concurrent-upload note). Add a per-book lock/advisory-lock if multi-trigger reprocessing ever lands.
- **A crash between the book row commit and the Hangfire enqueue orphans the book (S2.1).** `BookImportService` commits the `books` row, then enqueues `ProcessBookJob` in Hangfire's separate storage/transaction; a process death in between leaves the book `Uploaded` with no job, and re-uploading dedups (no re-enqueue) so it never reprocesses. No outbox/reconciliation sweep yet — over-engineering for the single-operator MVP, but a "requeue stuck-Uploaded books" admin action or startup sweep is the eventual fix.

Resolved:
- **The `auth.bearerToken` rotation question dissolved (ADR-0024).** The shared token is gone; admin is a per-user role on the cookie session, and the credential is a per-user password with the existing set/reset flow.
- **"Chat uses the new prompt" (S0.5.3 acceptance) — verified in S4.1.** `AssistantService` is the first consumer of `prompts.qa`: it reads the live setting via `settings.GetString("prompts.qa")` on every call and substitutes `{currentSection}`/`{currentLocation}`/`{question}`/`{context}`, so an admin PATCH to the prompt takes effect on the next `/ask` with no restart (the PATCH→live-value half was already verified in S0.5.3).
- **S3.1 resting status (settled with the user before coding): a chunked book rests at `Embedding`.** The issue text named a status (`Indexed-without-vectors`) that doesn't exist in the enum. Chosen for a monotonic progression (`Parsing→Parsed→Chunking→Embedding`) that signals "chunked, embedding pending" and is forward-compatible: S3.2's `EmbeddingJob` simply moves `Embedding→Indexed`. No new enum member; the frontend already styles `Embedding` and treats only `Indexed`/`Failed` as terminal (so the library keeps polling a chunked-but-unembedded book, which is correct — it *is* mid-pipeline). Caveat: no embedding job actually runs until S3.2, so the status leads the work by one slice.
- **S2.2 decisions (settled with the user before coding):** (1) the reader paginates **per-paragraph** (clean offsets) rather than off the whole-section `html` — accepts the mixed-block lead-in gap above; (2) pages are **CSS multi-column + native scroll with JS snap-on-settle** (pure CSS scroll-snap can't target anonymous column boxes); (3) reader settings persist in **Dexie now** and (4) the whole book is **prefetched** into Dexie on open — both (3) and (4) deliberately pull S2.3's cache foundation forward into S2.2, and S2.3 was rescoped to the remaining PWA/offline-state layer (`docs/issues.md`).
- **S2.1 decisions (settled with the user before coding):** (1) paragraph offsets index the **plain-text** rendering, not the XHTML; (2) covers are stored **as-extracted** (`cover.{ext}`) with content-type inferred from the extension — no image-processing dependency; (3) inline images are **extracted, served, and `src`-rewritten** (option C) rather than dropped, since the target books carry meaningful art; (4) the `/hangfire` dashboard uses **HTTP Basic auth** (password = bearer token) because browsers won't attach a custom `Authorization: Bearer` header.
- **Cost-rate setting shape (settled in S0.5.5): keep the JSON blob.** `llm.costRates` stays a single JSON setting keyed by `"<provider>:<model>"` (e.g. `"ollama:llama3.2:3b"`, `"openai_compatible:gpt-4o"` — the provider segment is the client's `ProviderName`) → `{ inputPerMillion, outputPerMillion }`. Flat per-rate keys (`llm.costRates.openai.gpt-4o`) were rejected: the seeder owns key creation and the admin UI only edits existing keys, so flat keys couldn't add a new model's rate without a reseed, whereas the blob is editable whole via the JSON settings editor. The rate object's JSON field names are now pinned camelCase via `[JsonPropertyName]` on `LlmCostRate` (see the S0.5.5 status note) so operator-typed camelCase binds correctly. Default seed is still `{}`; cost is null per row until a rate is configured.
- Branch renamed to `master` (matches conventions + CI triggers). Node engine widened to `>=22`; CI runs on Node 24 to match local dev.
- Reverse-proxy wiring: base compose is standalone (publishes a host port); a new `docker-compose.proxy.yml` overlay handles shared-network proxies (caddy/Traefik). Removed two mechanisms `docs/deployment/03` documented that Compose can't do (env-switched network membership, JSON-merged labels). `STORYMARK_PROXY_NETWORK` / `STORYMARK_PUBLIC_HOSTS` are now consumed by the overlay; docs 02/03 + `.env.example` reconciled.

## Recent decisions

Newest first. Full record in `docs/adr/`.

- ADR-0027 — One dark-mode authority: the UI (admin included) keys off `data-theme`, with Tailwind utilities built on PrimeVue tokens.
- ADR-0026 — Relaxed EPUB parsing for uploads, so real-world production EPUBs (e.g. quirky EPUB 2 NCX files) import instead of 500ing.
- ADR-0025 — Internet exposure hardening: forwarded TLS headers, `Secure` cookies, login throttling + per-account lockout. The VPN/LAN-only assumption from ADR-0023 is retired.
- ADR-0024 — Admin access via a per-user `is_admin` flag on the cookie session; the shared bearer token (and its settings row, seed env, and localStorage storage) is removed. Supersedes the admin-token aspect of ADR-0023.
- ADR-0018 — Per-book sequential `SectionExtractionJob`, run before embedding. One job per book iterating sections in order (not one job per section) avoids racing the `unique(book_id, canonical_name)` reconciliation index; extraction-before-embedding keeps `EmbeddingJob` as the unchanged terminal/reindex step. Both deviate from plan §15.2/§15.4 wording; confirmed with the user. **S5.2 extended the chain** the same way — `BuildingMemorySnapshots` (`BookMemorySnapshotJob`) sits between extraction and embedding (memory needs only summaries, not vectors), so embedding stays terminal.
- ADR-0017 — Fully custom touch selection (supersedes ADR-0015's mechanics). On coarse pointers `PaginatedSection.vue` owns the whole gesture (`user-select:none` + long-press word-snap via `Intl.Segmenter` + `caretPositionFromPoint` extend + custom drag-handles), removing the native-selection flash and making selections resizable; clamped to the current page, multiline within it. Popover gains **Copy**. Desktop keeps native selection.
- ADR-0014 — In-place reindex, dimensionless `vector` column, no HNSW (supersedes ADR-0010's coexistence mechanics; the no-silent-degradation intent stands). Enables any-dimension model swaps at single-operator scale.
- ADR-0013 — SPA served by the API as static files (same-origin, single image); the API image now carries the built Vue app in `wwwroot`.
- ADR-0012 — Automated tests deferred until after Phase 6.
- ADR-0011 — LLM call log + cost meter as core admin surfaces.
- ADR-0010 — pgvector with version-tagged embeddings (not Qdrant).
- ADR-0009 — Single-pass per-section extraction (summary + events + relationships in one LLM call).
- ADR-0008 — Settings persisted in Postgres with hot reload via `LISTEN/NOTIFY`.
- ADR-0007 — Per-task LLM model routing via `IChatModelClient`.
- ADR-0006 — Dexie/IndexedDB for offline cache (whole-book download on first open).
- ADR-0005 — PWA-first, Capacitor wrap-up in Phase 7.
- ADR-0004 — Hangfire on Postgres for background jobs.
- ADR-0003 — pgvector inside Postgres, not Qdrant.
- ADR-0002 — oxlint only (no ESLint).
- ADR-0001 — pnpm workspaces with an exact-version catalog.

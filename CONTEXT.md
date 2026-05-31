# Storymark — Current Context

A live pointer to where the project is right now. Keep it short. Move stable content into `docs/` and delete it from here.

Last updated: S0.5.4 landed.

## Status

- **S0.1 done** — Vue 3 + Vite + TS (strict) app boots; `/` Library placeholder, `/admin` PrimeVue shell (lazy). Tailwind v4 with light/sepia/dark tokens. `pnpm lint`, `format:check`, `typecheck`, `build` all pass. `packages/shared-types` wired as a source workspace package.
- **S0.2 done** — `apps/storymark-api` on `net10.0`: FastEndpoints + EF Core (Npgsql, no migrations) + Serilog JSON. `GET /api/health` → `{ status, version }`, verified locally; multi-stage non-root Dockerfile (build context = repo root).
- pnpm catalog locked to current published versions (verified via `pnpm view`). Note the majors: Vite 8, Vue Router 5, TypeScript 6.
- `docs/plan.md` and `docs/issues.md` capture the full scope through Phase 6.
- **S0.3 done** — `docker compose up` brings api + pgvector-postgres + ollama healthy. Local override (`docker-compose.override.yml`) builds the api image from source; postgres init script enables the `vector` extension (verified `vector 0.8.2`). `SELECT 1` and `ollama list` confirmed.
- **S0.5.1 done** — `settings` table (jsonb) + `settings_notify` trigger; `SettingsService` (singleton cache) hot-reloads via a `LISTEN settings_changed` BackgroundService, so even raw psql edits propagate (<1s, verified). AES-256-GCM encrypts `is_secret` values (`auth.bearerToken` ciphertext at rest, verified). Startup runs `MigrateAsync` then seeds missing keys (prompts from `Prompts/PromptDefaults.cs`, rest from the `SettingsDefaults` config section, `auth.bearerToken` from `Auth:BearerTokenSeed`). Env-gated `GET /api/_debug/setting?key=`.
- CI dotnet job now reads the SDK from `apps/storymark-api/global.json`.
- Note: as of S0.5.1 the API runs EF migrations on startup, so it now requires Postgres reachable at boot (the `/api/health` handler itself still doesn't touch the DB).
- **S0.5.4 done** — Admin shell UI (first frontend slice that consumes the admin API). `/admin` is a layout shell (PrimeVue `Menubar` + sidebar) with lazy nested routes `/admin/settings` and `/admin/prompts`; a bearer-token gate (stored in `localStorage`, validated against `GET /admin/settings`) wraps it. Settings view: PrimeVue `DataTable` grouped by namespace, type-aware inline edit (text/number/checkbox/JSON), `type` read-only (immutable server-side), secrets shown `••••` and **write-only** (edit sets a new value; Save disabled while a secret's field is blank, so it can't be accidentally cleared). Prompts view: list + monospace `Textarea` with save/revert. Data layer: `src/api/admin-api.ts` (+ `admin-types.ts`), `src/composables/use-admin.ts` (module-level shared state) and `use-admin-error.ts` (401 → sign out + toast). Vite dev proxies `/api` → `http://localhost:8088` so the app uses same-origin relative paths (no CORS; matches prod behind the reverse proxy). Toasts on save success/failure. Verified: gates green (lint/format/typecheck/build); dev server serves `/admin`; proxy forwards `/api`; the UI's exact PATCH operations swap `llm.chat.taskMap.qa` (next `qa` chat call used the new model, no restart) and edit `prompts.qa`. Prompts editor uses a PrimeVue `Textarea`, not CodeMirror 6 (deferred — see open questions).
- **S0.5.3 done** — Bearer-token auth + admin API. `/api/admin/*` is a FastEndpoints group (`Infrastructure/Auth/AdminGroup`) gated by `AdminAuthPreProcessor`: it constant-time-compares the `Authorization: Bearer` token against the live `auth.bearerToken` setting (fails closed — missing/empty token → 401, never a bypass), and stashes a SHA-256 token fingerprint that PATCH writes to `updated_by`. The group is `AllowAnonymous()` to ASP.NET (no auth middleware wired); the pre-processor is the gate. Endpoints: `GET/PATCH /api/admin/settings` (secrets masked; **type is immutable via the API** — only the value changes, validated against the stored type; secrecy preserved + re-encrypted), `GET/PATCH /api/admin/prompts[/{key}]`, `GET /api/admin/llm-calls?task=&limit=` (limit capped 1..200). `ISettingsService.UpdateAsync` is the write path (edits existing keys only; the seeder still owns key creation, type, and secrecy) and refreshes the cache immediately. Verified: 401 unauth / 200 auth; secret PATCH stays ciphertext at rest + masked; type-change rejected; prompt PATCH roundtrips and is live on the next read; `updated_by` = token fingerprint.
- **S0.5.2 done** — `IChatModelClient` (`Infrastructure/Llm/`) with `OllamaClient` (`/api/chat` + `/api/embed`) and `OpenAiCompatibleClient` (custom base URL + secret API key, read live from settings). `ChatModelRouter` (`Services/Llm/`) resolves a task → `provider:model` from settings on every call (so model swaps apply without restart) and routes through `LlmCallRecorder` — the single persistence chokepoint that writes one `llm_calls` row per call (prompt, response, tokens, latency, cost estimate from `llm.costRates`). New `llm_calls` table + `llm_calls_task_time` index (plan §12.16). Env-gated `POST /api/_debug/chat?task=`. Verified: a chat call returns and adds exactly one row with non-null latency/tokens; swapping `llm.chat.taskMap.qa` via psql is reflected on the next call with no restart. Cost is null for local Ollama (no rate seeded).

## In flight

Nothing. Next slice to grab is **S0.5.5** (LLM call log + cost meter views — adds the `GET /api/admin/llm-calls/cost-summary` endpoint and the call-log/cost admin views). See `docs/issues.md`.

## Conventions snapshot

The full conventions live in `docs/conventions/`. The short version:

- Strict TypeScript; no `any`, no `as any`, no `!`.
- oxlint only; lint errors fail CI.
- Exact-pinned versions everywhere (pnpm catalog for JS, `Directory.Packages.props` for .NET).
- No automated tests until after Phase 6 (`docs/adr/0012-tests-deferred.md`). Manual verification per issue.
- Vertical slices only. PR body must include the Acceptance Criteria reproduction steps.
- Conventional Commits.

## Open questions

- **Cost-rate setting shape (revisit in S0.5.5).** S0.5.2 stores rates as a single JSON setting `llm.costRates`, an object keyed by `"<provider>:<model>"` (e.g. `"openai_compatible:gpt-4o"`) → `{ inputPerMillion, outputPerMillion }`. The issue spec literally wrote per-key paths (`llm.costRates.openai.gpt-4o`), and note the provider segment is `openai_compatible` (the client's `ProviderName`), not `openai`. Default seed is `{}`, so cost is null until configured and the shape can change with no data migration. The cost-meter consumer (S0.5.5) and the per-key admin settings editor (S0.5.4) should settle whether to keep the JSON blob or move to flat per-rate keys to match the `llm.chat.taskMap.<task>` convention.
- **Failed LLM calls aren't audited.** `LlmCallRecorder` writes a row only on a successful invocation; a thrown call (timeout, 5xx, empty body) propagates without a row. Auditing failed attempts (which may still have sent a prompt to an external provider) needs a status/error column on `llm_calls` — deferred to its own slice.
- **"Chat uses the new prompt" (S0.5.3 acceptance) is verified in S4.1.** Nothing consumes `prompts.qa` yet — `_debug/chat` sends a raw prompt, and `prompts.*` templating lands with `AssistantService` (S4.1). S0.5.3 verified the PATCH→live-value half (GET + debug-setting reflect it immediately); the "next chat call uses it" half rides on S4.1's acceptance.
- **Prompts editor uses a plain `Textarea`, not CodeMirror 6 (S0.5.4 issue text).** CodeMirror would add several exact-pinned deps for what is plain text with `{placeholder}` tokens. Revisit if/when prompt editing wants placeholder highlighting or larger-document ergonomics.
- **`auth.bearerToken` is editable via the generic `PATCH /api/admin/settings`.** A holder of the current token can rotate the credential (or any secret) through the same masked endpoint — fine for a trusted single-operator panel and the only pre-UI rotation path, but worth a dedicated, audited token-rotation flow (and possibly excluding `auth.*` from the generic PATCH) when the admin UI lands (S0.5.4+).

Resolved:
- Branch renamed to `master` (matches conventions + CI triggers). Node engine widened to `>=22`; CI runs on Node 24 to match local dev.
- Reverse-proxy wiring: base compose is standalone (publishes a host port); a new `docker-compose.proxy.yml` overlay handles shared-network proxies (caddy/Traefik). Removed two mechanisms `docs/deployment/03` documented that Compose can't do (env-switched network membership, JSON-merged labels). `STORYMARK_PROXY_NETWORK` / `STORYMARK_PUBLIC_HOSTS` are now consumed by the overlay; docs 02/03 + `.env.example` reconciled.

## Recent decisions

Newest first. Full record in `docs/adr/`.

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

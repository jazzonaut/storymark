# Storymark — Current Context

A live pointer to where the project is right now. Keep it short. Move stable content into `docs/` and delete it from here.

Last updated: S0.1 + S0.2 landed.

## Status

- **S0.1 done** — Vue 3 + Vite + TS (strict) app boots; `/` Library placeholder, `/admin` PrimeVue shell (lazy). Tailwind v4 with light/sepia/dark tokens. `pnpm lint`, `format:check`, `typecheck`, `build` all pass. `packages/shared-types` wired as a source workspace package.
- **S0.2 done** — `apps/storymark-api` on `net10.0`: FastEndpoints + EF Core (Npgsql, no migrations) + Serilog JSON. `GET /api/health` → `{ status, version }`, verified locally; multi-stage non-root Dockerfile (build context = repo root).
- pnpm catalog locked to current published versions (verified via `pnpm view`). Note the majors: Vite 8, Vue Router 5, TypeScript 6.
- `docs/plan.md` and `docs/issues.md` capture the full scope through Phase 6.
- **S0.3 done** — `docker compose up` brings api + pgvector-postgres + ollama healthy. Local override (`docker-compose.override.yml`) builds the api image from source; postgres init script enables the `vector` extension (verified `vector 0.8.2`). `SELECT 1` and `ollama list` confirmed.
- **S0.5.1 done** — `settings` table (jsonb) + `settings_notify` trigger; `SettingsService` (singleton cache) hot-reloads via a `LISTEN settings_changed` BackgroundService, so even raw psql edits propagate (<1s, verified). AES-256-GCM encrypts `is_secret` values (`auth.bearerToken` ciphertext at rest, verified). Startup runs `MigrateAsync` then seeds missing keys (prompts from `Prompts/PromptDefaults.cs`, rest from the `SettingsDefaults` config section, `auth.bearerToken` from `Auth:BearerTokenSeed`). Env-gated `GET /api/_debug/setting?key=`.
- CI dotnet job now reads the SDK from `apps/storymark-api/global.json`.
- Note: as of S0.5.1 the API runs EF migrations on startup, so it now requires Postgres reachable at boot (the `/api/health` handler itself still doesn't touch the DB).

## In flight

Nothing. Next slice to grab is **S0.5.2** (IChatModelClient + Ollama/OpenAI adapters + LlmCallRecorder). See `docs/issues.md`.

## Conventions snapshot

The full conventions live in `docs/conventions/`. The short version:

- Strict TypeScript; no `any`, no `as any`, no `!`.
- oxlint only; lint errors fail CI.
- Exact-pinned versions everywhere (pnpm catalog for JS, `Directory.Packages.props` for .NET).
- No automated tests until after Phase 6 (`docs/adr/0012-tests-deferred.md`). Manual verification per issue.
- Vertical slices only. PR body must include the Acceptance Criteria reproduction steps.
- Conventional Commits.

## Open questions

None at the moment.

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

# Storymark — Current Context

A live pointer to where the project is right now. Keep it short. Move stable content into `docs/` and delete it from here.

Last updated: S0.1 + S0.2 landed.

## Status

- **S0.1 done** — Vue 3 + Vite + TS (strict) app boots; `/` Library placeholder, `/admin` PrimeVue shell (lazy). Tailwind v4 with light/sepia/dark tokens. `pnpm lint`, `format:check`, `typecheck`, `build` all pass. `packages/shared-types` wired as a source workspace package.
- **S0.2 done** — `apps/storymark-api` on `net10.0`: FastEndpoints + EF Core (Npgsql, no migrations) + Serilog JSON. `GET /api/health` → `{ status, version }`, verified locally; multi-stage non-root Dockerfile (build context = repo root).
- pnpm catalog locked to current published versions (verified via `pnpm view`). Note the majors: Vite 8, Vue Router 5, TypeScript 6.
- `docs/plan.md` and `docs/issues.md` capture the full scope through Phase 6.
- CI dotnet job now reads the SDK from `apps/storymark-api/global.json`.

## In flight

Nothing. Next slice to grab is **S0.3** (Docker Compose stack lights up) — see `docs/issues.md`.

## Conventions snapshot

The full conventions live in `docs/conventions/`. The short version:

- Strict TypeScript; no `any`, no `as any`, no `!`.
- oxlint only; lint errors fail CI.
- Exact-pinned versions everywhere (pnpm catalog for JS, `Directory.Packages.props` for .NET).
- No automated tests until after Phase 6 (`docs/adr/0012-tests-deferred.md`). Manual verification per issue.
- Vertical slices only. PR body must include the Acceptance Criteria reproduction steps.
- Conventional Commits.

## Open questions

- **Default branch name.** This repo's default branch is `main`, but `docs/conventions/05-commit-and-pr.md` and `.github/workflows/ci.yml` (push/PR triggers + image/deploy gates) all say `master`. As-is, CI never fires on `main`. Pick one: rename the branch to `master`, or switch the convention + CI triggers to `main`. Until resolved, CI is effectively dormant.
- **Node engine range.** `package.json` pins `engines.node` to `>=22 <23`, but local dev is on Node 24 (LTS) and CI on Node 22, so `pnpm install` warns "Unsupported engine". Decide whether to widen the range (e.g. `>=22`) and align CI's `node-version`, or hold dev to Node 22.

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

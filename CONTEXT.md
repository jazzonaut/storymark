# Storymark — Current Context

A live pointer to where the project is right now. Keep it short. Move stable content into `docs/` and delete it from here.

Last updated: pre-Phase-0 scaffold.

## Status

- Repository initialised. No application code yet.
- `docs/plan.md` and `docs/issues.md` capture the full scope through Phase 6.
- CI workflow scaffolded under `.github/workflows/ci.yml`; will be exercised once `apps/storymark-app` and `apps/storymark-api` are scaffolded by S0.1 / S0.2.
- `infra/compose/docker-compose.yml` is the reference Compose stack. Reverse-proxy and auth layers are pluggable per `docs/deployment/`.

## In flight

Nothing. Next slice to grab is **S0.1** (`docs/issues.md`).

## Conventions snapshot

The full conventions live in `docs/conventions/`. The short version:

- Strict TypeScript; no `any`, no `as any`, no `!`.
- oxlint only; lint errors fail CI.
- Exact-pinned versions everywhere (pnpm catalog for JS, `Directory.Packages.props` for .NET).
- No automated tests until after Phase 6 (`docs/adr/0012-tests-deferred.md`). Manual verification per issue.
- Vertical slices only. PR body must include the Acceptance Criteria reproduction steps.
- Conventional Commits.

## Open questions

None at the moment — every decision through the scaffold is captured in `docs/adr/`.

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

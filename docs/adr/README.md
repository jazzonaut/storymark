# Architecture Decision Records

One file per decision that's costly to revisit. Numbered, never renumbered. Status is one of `accepted`, `superseded`, `deprecated`.

If a decision is small enough that "look at the code" answers it, it's not an ADR.

## Index

| #    | Title                                                          | Status   |
| ---- | -------------------------------------------------------------- | -------- |
| 0001 | [pnpm workspaces with exact-pinned catalog](0001-pnpm-monorepo.md) | accepted |
| 0002 | [oxlint only — no ESLint](0002-oxlint-only.md)                 | accepted |
| 0003 | [pgvector inside Postgres — not Qdrant](0003-pgvector-not-qdrant.md) | accepted |
| 0004 | [Hangfire on Postgres for background jobs](0004-hangfire-on-postgres.md) | accepted |
| 0005 | [PWA-first, Capacitor in Phase 7](0005-pwa-first-capacitor-later.md) | accepted |
| 0006 | [Dexie/IndexedDB for offline cache](0006-dexie-offline-cache.md) | accepted |
| 0007 | [Per-task LLM model routing](0007-per-task-llm-model-routing.md) | accepted |
| 0008 | [Settings in Postgres with hot reload](0008-settings-in-db-with-hot-reload.md) | accepted |
| 0009 | [Single-pass per-section extraction](0009-single-pass-section-extraction.md) | accepted |
| 0010 | [Version-tagged embeddings + warn-reindex flow](0010-version-tagged-embeddings.md) | superseded by 0014 |
| 0011 | [LLM call log + cost meter](0011-llm-call-log.md)              | accepted |
| 0012 | [Tests deferred until after Phase 6](0012-tests-deferred.md)   | accepted |
| 0013 | [SPA served by the API (same-origin, single image)](0013-spa-served-by-api.md) | accepted |
| 0014 | [In-place reindex, dimensionless vectors, no HNSW](0014-in-place-reindex-dimensionless-vectors.md) | accepted |
| 0015 | [Custom selection repaint to suppress mobile toolbar](0015-custom-selection-highlight.md) | superseded by 0017 |
| 0016 | [Provider/model registry for LLM routing](0016-provider-model-registry.md) | accepted |
| 0017 | [Fully custom touch selection](0017-fully-custom-touch-selection.md) | accepted |
| 0018 | [Per-book sequential extraction, before embedding](0018-extraction-pipeline-shape.md) | accepted |
| 0019 | [Adopt VueUse for DOM/lifecycle primitives](0019-vueuse-for-dom-primitives.md) | accepted |
| 0020 | [Reading-progress conflict policy: server receive-time wins](0020-reading-progress-conflict-policy.md) | accepted |
| 0021 | [Stateless assistant conversations, client-carried](0021-stateless-assistant-conversations.md) | accepted |
| 0022 | [Offline fallback may serve a cache row stamped ahead of the reader](0022-offline-ahead-fallback.md) | accepted |
| 0023 | [In-app identity via IdentityCore + cookie session; per-user books](0023-in-app-identity-cookie-auth.md) | accepted; admin-token aspect superseded by 0024 |
| 0024 | [Admin access via a per-user `IsAdmin` flag on the cookie session](0024-admin-via-user-role.md) | accepted |
| 0025 | [Internet exposure: forwarded TLS, Secure cookies, login throttle](0025-internet-exposure-hardening.md) | accepted |

## When to write one

Write an ADR when the decision:

- locks future work into an interface or pattern that's costly to undo,
- chooses A over a popular B for non-obvious reasons,
- introduces a new dependency that becomes load-bearing,
- or codifies a workflow rule that a new contributor would otherwise re-litigate.

Skip an ADR when:

- the choice is the obvious default and the code already documents it,
- it's a style preference (those live under [`../conventions/`](../conventions/)),
- it's about *current state* rather than a decision (those live in [`../../CONTEXT.md`](../../CONTEXT.md)).

## Template

```markdown
# NNNN — Title

**Status:** accepted | superseded by ADR-XXXX | deprecated
**Date:** YYYY-MM-DD

## Context
What forced the decision.

## Decision
What was decided, in active voice.

## Consequences
What we now have to live with (good and bad).

## Alternatives considered
Concretely, with the reason each was rejected.
```

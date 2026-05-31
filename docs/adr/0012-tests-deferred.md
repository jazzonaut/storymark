# 0012 — Automated tests deferred until after Phase 6

**Status:** accepted

## Context

Storymark is a solo project building a new product surface — the reader UX, the AI memory features, the admin panel — none of which has a stable contract yet. Several major API shapes (prompt structure, section-extraction JSON schema, vector retrieval signature) will probably change two or three times before they settle.

Writing tests against unstable contracts is double work: write the test, write the code, rewrite both when the contract changes. The classic counter-argument is "tests prevent regression while you change things" — but regression in this context means "the section recap card no longer renders," which is what a human notices in 5 seconds of running the app. The valuable tests come later, when the shape is fixed and the regressions are subtle.

The author has explicitly chosen to defer tests until after Phase 6.

## Decision

**No automated tests during Phase 0 → Phase 6.** No Vitest, no xUnit, no Testcontainers, no Playwright. Don't scaffold them, don't add the dev dependencies, don't write the configs.

Every PR is verified manually via the **Acceptance Criteria** in its issue. PR descriptions must include the reproduction steps that prove the slice works. CI runs lint + typecheck + build; it does not run tests because none exist.

A **spoiler-safety regression harness** ([`../issues.md#CC.3`](../issues.md)) is sketched but explicitly deferred. The interim safeguard is the LLM call log (ADR-0011) plus an admin filter for `used_future_content = true`.

## Consequences

- Faster iteration through volatile early phases.
- Genuine risk of regressions slipping through, especially in spoiler-safety. Mitigated by the call log being an audit trail in production.
- Re-introducing tests later means writing them against the *settled* code, which is the right time but also a real chunk of work — call it ~2-3 weeks for a reasonable backend test suite plus a Playwright smoke flow.
- New contributors need to understand that "no test for X" is a known state, not a missing nice-to-have. This file is the answer to "why isn't there a test?"

## Re-introduction plan (Phase 7+)

When tests come back, the priority order is:

1. **Spoiler-safety regression pack** — golden sample books + positions; assert no future content reaches any AI surface.
2. **Backend service tests** — deterministic services: chunking, spoiler-safe filter, structured-JSON parsing, settings/secret encryption, LLM call recording.
3. **Integration tests** — Testcontainers Postgres + pgvector + fake `IChatModelClient` + golden EPUB fixtures.
4. **Frontend composables** — Vitest on Dexie roundtrips, API clients, offline-queue replay.
5. **End-to-end smoke** — Playwright: import a small EPUB, read a section, ask a question, see a recap card.

Until then, the rule is simple: ship a slice, run through its Acceptance Criteria on a real device, paste the result into the PR body.

## Alternatives considered

- **Tests from day one** — the orthodox answer; rejected for the reasons above.
- **Smoke tests only, no unit tests** — tempting, but a Playwright smoke is a non-trivial scaffold of its own; deferring everything together is cleaner.
- **Test ADRs and conventions docs, not code** — not a thing.

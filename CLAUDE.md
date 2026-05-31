# Working with Storymark — instructions for Claude Code

This file is loaded into Claude Code's context for every session in this repo. Keep it lean and load-bearing; everything that isn't a *rule for how you work here* belongs in `docs/`.

## Read first

Before doing anything non-trivial:

1. `docs/vision-and-architecture.md` — what we're building and why.
2. `docs/plan.md` — the full implementation plan (long; skim the headings, then read the section you need).
3. `docs/issues.md` — the active backlog as vertical-slice issues.
4. `docs/adr/` — every "why this and not that" decision lives here. If a decision feels arbitrary, you're missing an ADR.
5. `docs/conventions/` — naming, code style, commit/PR shape. Non-negotiable.
6. `CONTEXT.md` — current state and what's in flight.

If the user asks for something that contradicts an ADR or convention, flag it before doing it.

## Hard rules

These are not preferences. Violations are bugs.

- **Strict TypeScript**: `"strict": true`, `"noUncheckedIndexedAccess": true`, `"exactOptionalPropertyTypes": true`. No `any`. No `as any`. No `!` non-null assertion. Model the type properly or accept the narrowing cost.
- **oxlint only** — never add ESLint. oxlint rulesets are configured strictly; lint errors fail CI.
- **Exact-pinned versions** — no `^` or `~` anywhere. Frontend deps go through the pnpm catalog (`pnpm-workspace.yaml`). Backend deps go through `Directory.Packages.props`. Bump intentionally, never opportunistically.
- **No tests right now** — automated tests are deferred until after Phase 6 (see `docs/adr/0012-tests-deferred.md`). Don't add Vitest, xUnit, Testcontainers, or any test scaffolding. Every PR is verified manually via the Acceptance Criteria in its issue.
- **No duplication, no workarounds, no lazy code**. If you're about to copy-paste a block, extract it. If you're about to add a fallback for a case that can't happen, don't. If you're about to wrap a strict check to avoid fixing the real problem, fix the real problem. The user has explicitly forbidden lazy patterns; this is enforced in code review.
- **No PII in committed files**. Examples use `example.com`, `forge.example.com`, `REGISTRY_HOST`, etc. Never commit the user's domain, paths, or any specific homelab detail discovered in their other repos. If the user wants production values, they go in `.env` (gitignored) or CI secrets.
- **Spoiler-safety is the most important product invariant** (`docs/plan.md` §17). Any LLM call site MUST go through `SpoilerSafeRetrievalService` for evidence and `LlmCallRecorder` for the call itself. Bypassing either is a bug, not a shortcut.
- **No comments that explain WHAT** the code does. Comments explain non-obvious WHY only — a hidden constraint, a workaround for a specific upstream bug, a subtle invariant. Default to no comment.

## Workflow conventions

- **Vertical slices, always.** Every change ships an end-to-end demonstrable behaviour. No "schema-only PR followed by a UI PR" splits unless each half is independently demoable.
- **Issues drive work.** When the user says "do S3.2", read `docs/issues.md` for that issue, then start. If an issue's scope feels wrong, raise it before coding.
- **Plan before broad changes.** For anything that touches more than ~3 files in non-obvious ways, sketch the approach to the user first. Small refactors don't need this.
- **Commit shape**: Conventional Commits (`feat: ...`, `fix: ...`, `chore: ...`, `docs: ...`, `refactor: ...`). See `docs/conventions/05-commit-and-pr.md`.
- **Branches**: short, kebab, prefixed by issue ID. `s3-2-embeddings-pgvector`, `s5-1-section-extraction`.
- **PRs**: title is a Conventional Commits line; body has a Summary and a Manual Verification section reproducing the issue's Acceptance Criteria (since we don't have tests yet).

## Tooling shortcuts

- `task ci` runs the same gates CI runs: `pnpm lint`, `pnpm typecheck`, `pnpm build`, `dotnet format --verify-no-changes`, `dotnet build -warnaserror`.
- `task dev` + `task api` for the dev loop.
- `task compose:up` for the local stack.

If a tool you'd reach for isn't in `Taskfile.yml` and is going to be useful repeatedly, add it there rather than scattering one-liners.

## Where to put new docs

- An "I made a decision" document → `docs/adr/NNNN-slug.md`. Pick the next number, link from `docs/adr/README.md`.
- A "how does this thing work?" or "what's the API of X" doc → `docs/` (top level) or a topic-specific subfolder.
- A "you must do X this way" rule → `docs/conventions/`.
- Anything that explains *current state* of an in-flight thing → `CONTEXT.md` (and remove from there when it ships).
- Nothing else at the repo root. Only `README.md`, `CLAUDE.md`, `CONTEXT.md`, and `LICENSE` live at root.

## Asking for help

If something is genuinely ambiguous (e.g. the issue under-specifies a UX detail), ask. Don't guess and ship — the user prefers a pause over rework.

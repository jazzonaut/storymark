# Commits and PRs

## Local gates — run before every push

Run **`task ci`** before pushing. It runs the exact gates CI runs, across both
halves of the stack:

- frontend: `pnpm lint`, `pnpm format:check`, `pnpm typecheck`, `pnpm build`
- backend: `dotnet format --verify-no-changes`, `dotnet build -c Release -warnaserror`

This is a rule, not a suggestion: a green `task ci` is the local equivalent of a
green CI run. We deliberately don't install a git hook to force it (keeps commits
fast and the toolchain dependency-free) — CI is the backstop, and a push that
fails CI blocks the deploy.

Note what is **not** a substitute for `task ci`:

- `task build` / `docker compose up --build` compile the code but don't run the
  frontend lint/typecheck/format or the `dotnet format` whitespace check. (The
  backend image build *does* fail on analyzer warnings and code-style, because
  `Directory.Build.props` sets `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild`.)
- Deployment itself is gated automatically: the CI image is built and pushed only
  after both the `web` and `dotnet` jobs pass, and the Portainer redeploy only
  after the image publishes.

## Branches

- `master` is the default branch and is always deployable.
- Feature branches are short, kebab-case, prefixed by the issue id:

```text
s3-2-embeddings-pgvector
s5-1-section-extraction
cc-1-ci-workflow
```

If a change has no issue (rare; only docs or trivial chores), prefix with the conventional-commits type:

```text
docs-update-deployment-guide
chore-bump-tailwind
```

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/). Required prefixes:

- `feat` — new user-facing capability
- `fix` — bug fix
- `refactor` — internal change with no user-visible effect
- `perf` — performance change
- `docs` — documentation only
- `chore` — tooling, deps, CI
- `build` — build system changes
- `revert` — revert of a previous commit

Subject:

- Imperative present tense ("add", not "added" or "adds").
- Lowercase first word after the colon.
- ≤ 72 characters.
- No trailing period.

Body (optional but encouraged for non-trivial changes):

- Wrapped at ~72 chars.
- Explains **why**, not **what** (the diff shows what).
- References the issue id (`S3.2`, `S5.1`) when there is one.

Example:

```text
feat(retrieval): hybrid pgvector + FTS spoiler-safe search

S3.3. Single SQL statement merges semantic (HNSW) and keyword (FTS)
hits filtered by (section_index, location_*) and the active
embedding_model_id. Returns top-K reranked, de-duped chunks ready
to feed into a prompt context window.
```

## PRs

One PR per issue. Title is the commit subject. Body:

```markdown
## Summary
<1-3 bullets on what landed and why>

## Issue
Closes #123.

## Manual verification
<Reproduction of the Acceptance Criteria from the issue.>

1. `task compose:up && task api && task dev`
2. POST `/api/_debug/search-safe ...`
3. Observe ...
```

Tests are deferred ([ADR-0012](../adr/0012-tests-deferred.md)); the Manual Verification section is the gate, not an optional add-on.

## Reviews

- Self-review the diff before requesting review. Read it as if you wrote it last week.
- A PR that touches more than ~400 lines is probably two PRs.
- "LGTM" without a verification note means the reviewer also ran the verification steps.

## Merge

- **Squash merge** to `master`. The squashed commit subject must be a valid Conventional Commit line — copy the PR title.
- Delete the branch after merge.
- Tag releases manually when shipping a milestone (`v0.1.0-phase-2`). No automated semver in v1.

## Reverts

- `git revert <sha>` on `master` is the standard recovery path. Don't force-push to undo.
- A revert PR is a normal PR with `revert:` type.

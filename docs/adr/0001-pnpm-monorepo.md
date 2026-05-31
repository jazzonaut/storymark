# 0001 — pnpm workspaces with exact-pinned catalog

**Status:** accepted

## Context

Storymark has a Vue 3 app, a (future) Capacitor wrapper, and a shared-types package consumed by both the frontend and tooling. Three workspaces minimum, more likely as the project grows. Dependency drift between workspaces is a known papercut on monorepos — `^` ranges allow different installs to resolve to different versions across packages and across developer machines.

## Decision

Adopt pnpm workspaces with a **catalog of exact-pinned versions** (`pnpm-workspace.yaml`). Workspace `package.json` files reference catalog entries as `"vue": "catalog:"`. Internal cross-package deps use `"workspace:*"`.

No `^`, no `~` anywhere — version bumps are deliberate edits to the catalog, performed by humans (or Renovate post-MVP) reading release notes.

## Consequences

- Same version of every dependency across the workspace; no resolver drift.
- Bumping is a visible PR, not a silent install difference.
- New workspace packages get the catalog for free.
- Slightly more friction to evaluate "what if we tried version X?" — but the answer is just one catalog edit + `pnpm install`.

## Alternatives considered

- **npm workspaces** — works, but pnpm's content-addressed store is materially faster on this size of monorepo and the catalog feature is unique to pnpm.
- **Single `package.json` at root, no workspaces** — punishes any future split (Capacitor wrapper, shared-types).
- **Nx / Turborepo** — overkill at three packages; revisit if the task graph gets complex enough to need caching across boundaries.
- **`^` ranges** — guarantees drift, encourages "works on my machine."

# `packages/shared-types`

TypeScript contracts shared between `apps/storymark-app` and any tooling that needs to talk to the API in TS.

Consumed as source
via `"@storymark/shared-types": "workspace:*"`; `src/index.ts` is the package's
public API (the one place a barrel file is allowed — see
[`docs/conventions/01-typescript.md`](../../docs/conventions/01-typescript.md)).

The contracts here are **hand-maintained** during MVP. Generation from the .NET OpenAPI document is a post-MVP option once the surface stabilises; the trade-off is whether hand-sync drift becomes more painful than introducing a code-gen step in CI. We'll write the ADR when it's a real choice.

# `packages/shared-types`

TypeScript contracts shared between `apps/storymark-app` and any tooling that needs to talk to the API in TS.

**Status:** placeholder. To be scaffolded by issue [`S0.1`](../../docs/issues.md).

Consumers reference this package as `@storymark/shared-types` via `"workspace:*"`.

The contracts here are **hand-maintained** during MVP. Generation from the .NET OpenAPI document is a post-MVP option once the surface stabilises; the trade-off is whether hand-sync drift becomes more painful than introducing a code-gen step in CI. We'll write the ADR when it's a real choice.

# 0002 — oxlint + oxfmt only (no ESLint, no Prettier)

**Status:** accepted

## Context

The TypeScript/Vue community defaults to ESLint + typescript-eslint, but ESLint is slow on larger workspaces and the rule-config surface has grown unwieldy. oxlint is a Rust-based linter compatible with most popular ESLint rules, an order of magnitude faster, and easier to configure from scratch.

Storymark has no existing ESLint setup to migrate, so the cost of "ESLint by default" doesn't apply.

## Decision

Use **oxlint exclusively** for linting and **oxfmt exclusively** for formatting. Strict oxlint ruleset: categories `correctness`, `suspicious`, `perf` all at `error`; plugins `typescript`, `unicorn`, `import`, `promise`, `vue`. Forbid `any`, `as any`, and `!` non-null assertions. Forbid default exports outside Vue SFCs / view modules / config files. Filenames must be kebab-case (PascalCase allowed for `.vue` components).

CI runs `oxlint .` and `oxfmt --check .` and fails on any error.

No Prettier. If oxfmt's coverage of a specific file shape lags (e.g. SFC handling rough edges, exotic Markdown corners), we either work around at the call site or contribute upstream — not by re-adopting Prettier.

## Consequences

- Fast feedback in editor and CI; one toolchain to configure; one editor plugin per developer.
- Format + lint share concepts and tags, so editor integration is uniform.
- Type-aware lint rules (the historical reason to keep ESLint around) are not yet first-class in oxlint. If a missing rule bites, we'll either suppress at the call site or add ESLint as a second linter for type-aware rules only — but not pre-emptively.
- oxfmt is younger than Prettier; if a formatting edge case is genuinely broken, the path is "report + work around," not "add Prettier back."

## Alternatives considered

- **ESLint + typescript-eslint** — slower, larger config, no upside for a greenfield project.
- **Prettier alongside oxlint** — the original plan; rejected because keeping two formatters around just to defer the oxfmt switch creates the same dual-config tax we're avoiding on the lint side.
- **Biome** — viable, but oxlint+oxfmt already cover the rule surface we care about and the project momentum is good.
- **Both ESLint and oxlint** — doubles config, doubles CI time, doubles editor plugins. Only acceptable if a specific rule forces it.

# TypeScript Conventions

These rules are enforced by `tsconfig.json` + oxlint where mechanically possible, and by code review otherwise. Violations are bugs.

## Compiler

Every `tsconfig.json` in the workspace inherits the strictest settings:

```jsonc
{
  "compilerOptions": {
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noImplicitOverride": true,
    "exactOptionalPropertyTypes": true,
    "noFallthroughCasesInSwitch": true,
    "noImplicitReturns": true,
    "forceConsistentCasingInFileNames": true,
    "isolatedModules": true,
    "skipLibCheck": true
  }
}
```

If `noUncheckedIndexedAccess` makes a line tedious, the line is wrong. Indexed access can fail; type the failure mode.

## Forbidden

- `any`. If a type is genuinely unknown, use `unknown` and narrow.
- `as any`. Same — model the type or narrow correctly.
- `!` non-null assertion. Use a check, a destructure default, or a refinement.
- `// @ts-ignore`. If a check is wrong, fix it or write a `// @ts-expect-error` with the reason and the link to the upstream issue.
- `as` casts away from a wider type. `as` is acceptable for *narrowing* (e.g. tagged-union refinement) or for `as const`. It is not a tool for shutting up the compiler.

## Required

- **Type imports** carry the `type` modifier (`import type { Foo } from '…'`). Enforced by oxlint.
- **`readonly`** on every field that isn't reassigned after construction. Default to immutable.
- **`readonly T[]` / `ReadonlyArray<T>`** for parameters and return types unless the caller is documented to mutate.
- **Discriminated unions** for state machines. Avoid optional fields that "go together" — model them as a tagged union.
- **Exhaustiveness** on `switch` over discriminated unions, via a `never`-typed default arm.

## Modules

- One module per file.
- **No default exports** outside Vue SFCs, view modules (`views/**/*.ts`), router modules, and Vite/oxlint config files. Named exports only — they survive renames and make import sites self-documenting.
- **Barrel files** (`index.ts` re-exporting everything in a folder) are forbidden for runtime modules. They hide circular deps and break tree-shaking. The exception is small package public APIs (`packages/shared-types/src/index.ts`).
- **Absolute imports** via `@/…` alias inside an app; cross-package imports via `@storymark/…`. Never `../../../`.

## Errors

- Throw `Error` subclasses, not strings.
- Define a typed error class per error domain (e.g. `UploadValidationError`, `LlmProviderError`).
- API client functions return `Promise<T>` — they throw on failure. Don't pre-emptively wrap in `Result<T, E>` types unless a specific call site needs both branches.

## Async

- `async`/`await` everywhere. Avoid `.then()` chains.
- `Promise<void>` for fire-and-forget — but actually `await` them or `void promise` deliberately. Floating promises are bugs (oxlint catches the common shapes).

## Naming inside types

- Type aliases: `PascalCase`.
- Type parameters: meaningful name (`TSection`, `TEntity`) over single letters when there's more than one.
- Boolean-valued fields: `isFoo`, `hasFoo`, `canFoo`. Never just `foo: boolean`.

## What not to model

- Don't pre-emptively model "future flexibility." A type union of one variant is fine. Add the second variant when it lands.
- Don't add optional fields for hypothetical extensions. Add them when they're real.

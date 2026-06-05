# Vue Conventions

Vue 3, Composition API, `<script setup>`, TypeScript. Tailwind for the reader; PrimeVue for the admin panel.

## Components

- Single-file components (`.vue`), one component per file.
- Filename **PascalCase** matching the component (`BookCard.vue`, `MemoryPanel.vue`). Enforced by oxlint via the per-file override.
- `<script setup lang="ts">` always.
- Component public surface = `defineProps` + `defineEmits` + named slots. No `attrs`-driven side channels.
- Props use `defineProps<{ … }>()` typed inline. No runtime validation wrappers.
- Emits use `defineEmits<{ (e: 'something', payload: Foo): void }>()`. Strongly typed; do not use the array form.
- Slot props are typed via `defineSlots<{ default: (props: { … }) => void }>()` when there are any.

## Structure inside `<script setup>`

A predictable order:

```ts
// 1. imports
// 2. props / emits / slots
// 3. local state (refs, reactives, computed)
// 4. lifecycle hooks
// 5. event handlers
// 6. expose (rare)
```

Don't intermix. A long `<script setup>` is a signal to split a component.

## State

- **No Pinia.** Module-level `ref`/`shallowRef`/`reactive` exposed through a `useFoo()` composable is the pattern. Single-user app, one store-shaped concept per domain entity, devtools value is marginal.
- **`shallowRef` over `ref`** for collections that you don't mutate in place. `Map`/`Set` of large objects → `shallowRef` + manual `triggerRef` after batch updates.
- **`markRaw`** on objects you'll never mutate (cached entity cards, ECharts option objects). Skips proxy overhead.
- **One owner per piece of state.** If two composables both write to the same ref, one of them is wrong; introduce a single owner and let the other read.

## Composables

- Filenames in `src/composables/` are kebab-case: `use-books.ts`. The exported function is `useBooks`. This split avoids the oxlint kebab-case-on-filename rule clashing with the PascalCase Vue ecosystem expectation.
- A composable that needs shared state declares it at module scope, not inside the function. The function returns the shared refs + actions.
- Composables don't touch the DOM directly. If you need a DOM ref, the calling component owns it and passes it in.
- One concern per composable. `useReader` does reader state; it does not fetch books. Cross-composable wiring happens in views.

## Styles

- Reader UI: **Tailwind utility classes** in template. No `<style scoped>` for layout/spacing/colour — only for cases Tailwind can't express cleanly (e.g. CSS column rules in `PaginatedSection.vue`).
- Admin UI: **PrimeVue components** for widgets; chrome around them uses the **`tailwindcss-primeui` semantic utilities** (`bg-surface-*`, `text-muted-color`, `border-surface`, …) so Tailwind styling stays bound to the active PrimeVue palette (ADR-0027). Don't use raw palette classes (`zinc-*`, `gray-*`) in admin chrome.
- No global CSS resets beyond what Tailwind provides.
- Theme tokens live in `src/styles/theme.css` (Tailwind v4, no `tailwind.config.ts`). Reader content themes (light/sepia/dark) swap the `data-theme` attribute on `<html>`; that same attribute drives Tailwind's `dark:` variant and PrimeVue's dark mode (ADR-0027). Sepia is reader-content-only — chrome renders light under it.

## Routing

- File-based routes via the `router/` directory + manual `routes` array. No auto-router for now — the route count is small enough to keep explicit.
- Lazy-load heavy routes (admin, reader, timeline, graph) with `() => import('…')`.

## Templates

- No `v-html` unless the value is freshly sanitised by the backend (specifically `SectionRenderer` rendering `section.html`). Document why at the call site.
- Prefer `v-if`/`v-else` over `v-show` unless toggling cheap elements very frequently.
- Don't put logic in templates beyond field access. Lift to a `computed`.
- Use `:key` on `v-for` always, and use a stable id, not the array index.

## Suspense, async setup

- Avoid `async setup()`. It complicates error boundaries and pre-render. Use loading state inside the component instead.

## Performance defaults

- `defineAsyncComponent` for components that are mounted occasionally (admin panels, dialogs).
- ECharts wrappers (`vue-echarts`) are loaded lazily and never on the reader's hot path.
- Animations are CSS-only unless interactivity demands otherwise.

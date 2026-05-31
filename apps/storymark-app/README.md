# `apps/storymark-app`

The Vue 3 PWA — reader + admin panel.

**Status:** scaffolded (issue [`S0.1`](../../docs/issues.md)). Vite + Vue 3 + TypeScript
(strict), Tailwind v4, PrimeVue v4, and Vue Router are wired. `/` is the Library
placeholder; `/admin` is the PrimeVue shell placeholder (lazy-loaded).

## Layout

```text
apps/storymark-app/
  package.json            # catalog deps as "vue": "catalog:"
  tsconfig.json           # extends @vue/tsconfig + ../../tsconfig.base.json (strict)
  vite.config.ts          # @vitejs/plugin-vue + @tailwindcss/vite, "@/" -> src alias
  index.html              # data-theme="light" on <html>
  env.d.ts
  src/
    main.ts               # app bootstrap (router + PrimeVue Aura preset)
    App.vue               # <RouterView /> shell
    router/               # manual routes array (lazy-load heavy routes)
    styles/theme.css      # Tailwind import + light/sepia/dark theme tokens
    views/                # LibraryView.vue, admin/AdminView.vue
```

Folders not yet present (`components/`, `composables/`, `api/`, `db/`, `sw/`,
`utils/`) land with the features that need them.

## Scripts

Run from the repo root: `pnpm dev`, `pnpm build`, `pnpm typecheck`, `pnpm lint`,
`pnpm format:check`.

See [`docs/conventions/02-vue.md`](../../docs/conventions/02-vue.md) for the Vue rules.

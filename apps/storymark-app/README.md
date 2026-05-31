# `apps/storymark-app`

The Vue 3 PWA — reader + admin panel.

**Status:** placeholder. To be scaffolded by issue [`S0.1`](../../docs/issues.md) in [`docs/issues.md`](../../docs/issues.md).

When scaffolded this directory will contain:

```text
apps/storymark-app/
  package.json            # references catalog deps as "vue": "catalog:"
  tsconfig.json           # strict (see docs/conventions/01-typescript.md)
  vite.config.ts
  tailwind.config.ts
  index.html
  src/
    main.ts
    App.vue
    router/
    assets/
    styles/
    components/           # Reader (Tailwind)
    admin/                # Admin (PrimeVue)
    views/
    composables/
    api/
    db/                   # Dexie schemas (Phase 2+)
    sw/                   # service worker (Phase 2+)
    types/
    utils/
```

See [`docs/conventions/02-vue.md`](../../docs/conventions/02-vue.md) for the Vue rules.

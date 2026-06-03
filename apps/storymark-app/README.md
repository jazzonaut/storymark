# `apps/storymark-app`

Vue 3 PWA for the Storymark reader and admin panel.

## Current Surface

- Library grid with EPUB upload, delete, install prompt, cached offline library fallback, and processing status polling.
- Paginated reader with typography/theme controls, wake lock, bookmarks, backend-synced progress, offline section cache, and inline resource image cache warming.
- Spoiler-safe memory surfaces in the reader: Summary, Timeline, Graph, Story Guide, Assistant, entity popups, selection hints, and recap cards.
- Admin panel for settings, prompts, model registry, LLM call log, cost meter, books maintenance, section-title backfill, stuck-upload recovery, and full book reprocessing.

## Layout

```text
src/
  api/             browser API clients and read-through cache helpers
  components/      shared UI plus reader/admin components
  composables/     Vue state modules for reader, admin, offline sync, cache, memory
  db/              Dexie schema and local cache cleanup helpers
  router/          library, upload, reader, and admin routes
  styles/          Tailwind import and Storymark theme tokens
  utils/           reader formatting, color, entity highlighting, labels
  views/           route-level screens
```

## Run

From the repo root:

```bash
pnpm dev
pnpm typecheck
pnpm lint
pnpm format:check
pnpm build
```

Vite proxies `/api` to `http://localhost:8088` in dev and preview. The production SPA is served by the API image from the same origin.

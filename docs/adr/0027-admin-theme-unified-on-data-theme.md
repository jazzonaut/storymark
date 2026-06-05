# 0027 — UI dark mode keyed to `data-theme`, PrimeVue-token Tailwind utilities

**Status:** accepted
**Date:** 2026-06-05

## Context

The app has had three dark-mode authorities that could disagree:

1. The app theme — `data-theme` on `<html>` (light/sepia/dark), chosen in the
   reader settings drawer, persisted in Dexie, and driving the `--sm-*` page
   tokens in `theme.css`.
2. Tailwind's `dark:` variant, which defaulted (Tailwind v4) to the OS
   `prefers-color-scheme`.
3. PrimeVue Aura's dark mode, which also defaulted to the OS preference.

Pick dark in the reader on a light-OS machine and the admin rendered a dark
page background under light PrimeVue components — and vice versa. On top of
that, `data-theme` was only applied when `ReaderView` mounted, so a direct
navigation to `/admin` or the library never applied the persisted theme at all.
Admin chrome also styled itself with raw Tailwind `zinc-*` classes while
PrimeVue components used Aura's own surface palette, so the two could drift
even when the mode agreed.

## Decision

- **`data-theme` is the single dark-mode source of truth.** Tailwind's `dark:`
  variant is redefined as `@custom-variant dark
  (&:where([data-theme="dark"], [data-theme="dark"] *))` and PrimeVue gets
  `darkModeSelector: '[data-theme="dark"]'`. The OS preference plays no role.
- **Sepia is a reader-content theme, not a chrome theme.** It matches neither
  selector, so UI chrome (admin, dialogs, tables) renders light while reader
  content keeps its `--sm-*` sepia tokens.
- **The persisted theme is applied at boot**: `App.vue` calls
  `useReaderSettings().load()`, so every route gets the chosen theme, not just
  the reader. (This also restores the fullscreen preference at boot, which the
  settings module was already designed to do across reloads.)
- **`tailwindcss-primeui` bridges the token gap**: Tailwind utilities
  (`bg-surface-*`, `text-muted-color`, `border-surface`, …) generated from the
  active Aura palette replace raw `zinc-*` classes in admin chrome, so Tailwind
  styling and PrimeVue components share one palette in both modes.
- ECharts (cost chart) mirrors the same source: the wrapper switches to the
  built-in `dark` theme when `data-theme` is dark.

## Consequences

- Theme switching is deterministic: one attribute flips Tailwind, PrimeVue,
  and charts together, and a fresh tab on any route honors it.
- The admin no longer follows the OS theme. An operator whose OS is dark but
  whose stored app theme is light gets a light admin — accepted, the in-app
  choice is the explicit one.
- `tailwindcss-primeui` (exact-pinned) becomes load-bearing for admin styling.
- The reader's own surfaces keep using `--sm-*` tokens; nothing changes there.

## Alternatives considered

- **Follow the OS everywhere** (drop `data-theme` for chrome) — rejected: the
  reader's explicit light/sepia/dark choice is a core reading feature, and the
  page background driven by `--sm-bg` would still clash with OS-driven chrome.
- **Per-area theming** (admin follows OS, reader follows `data-theme`) — two
  sources of truth again, plus the global `--sm-bg` page background bleeds into
  admin; rejected as the bug with extra steps.
- **Hand-maintained zinc `dark:` classes** (status quo, fixed selectors only) —
  keeps two palettes that drift apart from Aura's surfaces; the official bridge
  plugin removes the drift for one small pinned dependency.

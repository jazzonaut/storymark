# 0019 — Adopt VueUse for DOM/lifecycle primitives

**Status:** accepted
**Date:** 2026-06-01

## Context

The reader and admin UIs had accumulated a layer of hand-rolled browser-API
plumbing: `addEventListener`/`removeEventListener` pairs split across
`onMounted`/`onBeforeUnmount`, a module-level `online`/`offline` listener block, a
90-line focus trap, a document-level pointerdown "click outside" dismiss, manual
`setTimeout` debounce/throttle bookkeeping, a `ResizeObserver`, and drag-and-drop
file handling. Each is a well-understood primitive with well-understood failure
modes (mismatched listener removal, capture-flag drift, focus-restore edge cases),
and each was re-implemented per call site.

This is exactly the surface [VueUse](https://vueuse.org) exists to cover:
scope-aware composables that register and tear down browser APIs on the owning
component's lifecycle, with the edge cases already handled and exercised by a
large user base.

## Decision

Adopt `@vueuse/core` as the standard source for DOM- and lifecycle-bound
primitives. Reach for it before hand-rolling `addEventListener`, observers,
media-query reads, click-outside, debounce/throttle, storage, or rAF loops.

Concretely, this replaces:

- `useOnline` — the module-level online/offline block in `use-offline-sync.ts`.
- `useEventListener` — manual listener add/remove pairs (`ReaderView`,
  `EntityPopup`, `SelectionPopover`, `PaginatedSection`).
- `onKeyStroke` — Escape-to-close handling. Rather than repeat it per overlay, it
  is wired once inside our `useFocusTrap` wrapper via an optional `onClose`
  callback (the trap's `active` ref is already the overlay's open state), so every
  trapped modal — including `ContentsPanel`, which previously lacked it — gets
  consistent Escape-to-close for free.
- `onClickOutside` — the pointerdown + `contains()` dismiss in `SelectionPopover`
  (its `ignore` option models the selection-handle exception we coded by hand).
- `useDropZone` / `useFileDialog` — the drag state and hidden-input handling in
  `BookUploadView`.
- `useResizeObserver`, `useMediaQuery`, `useDebounceFn`, `useTimeoutFn`,
  `useRafFn` — observer, pointer-coarse query, debounce/throttle, and the
  page-turn animation in `PaginatedSection` / `ReaderView`.

We also adopt `@vueuse/integrations`' `useFocusTrap` (which wraps the
`focus-trap` library) in place of our own focus trap. Our `useFocusTrap(active,
container, onClose?)` composable is kept as a thin wrapper over it, so the
dependency stays behind one seam; the optional `onClose` folds Escape-to-close
into the same call (see the `onKeyStroke` note above). Escape stays *our* code via
`onKeyStroke`, not the focus-trap library's `escapeDeactivates` (left `false`), so
it cannot fight the scrim click or the library's own key handling.

What we deliberately **do not** route through VueUse: anything persisting to
Dexie/IndexedDB (`use-reader-settings`, `use-memory-panel`, `use-previously`,
`use-reading-progress`) stays as-is — `useStorage` is `localStorage`/
`sessionStorage` only and IndexedDB is the deliberate store (ADR 0006). The
bespoke touch-selection gesture engine in `PaginatedSection` (ADR 0017) stays
hand-written; only its surrounding observers/listeners/timers move to VueUse.

Versions are catalog-pinned exactly per ADR 0001: `@vueuse/core` and
`@vueuse/integrations` at `14.3.0`, `focus-trap` at `8.2.1`.

## Consequences

- Less hand-maintained browser-API code, and the listener/observer teardown is no
  longer our responsibility to get right per call site.
- The modal panels (Bookmarks, Settings, Memory, Contents) gain consistent
  Escape-to-close, closing a real a11y gap rather than just deduplicating.
- Three new runtime dependencies. `@vueuse/core` is tree-shakeable, so only the
  composables we import ship. `@vueuse/integrations` is imported only via its
  `useFocusTrap` subpath, which pulls in `focus-trap`.
- The focus-trap swap is the one behavioural change to verify by hand (initial
  focus, Tab cycling, focus restore, outside-click and Escape interplay) since we
  no longer own that code. Kept behind the existing wrapper so it is trivially
  revertible.
- VueUse tracks Vue's reactivity closely; a Vue bump may now also imply a VueUse
  bump. Both are catalog-pinned, so that is a deliberate edit, not a surprise.

## Alternatives considered

- **Keep hand-rolling.** Zero new dependencies, but we keep paying for and
  re-verifying primitives that are not our problem to solve, and the focus trap in
  particular is subtle a11y code we'd rather not own. Rejected.
- **Cherry-pick by copying VueUse source** for the few helpers we use. Avoids the
  dependency but forks code we then can't update, and loses tree-shaking's
  practical equivalent (we'd copy whatever we touch). Rejected.
- **`@vueuse/core` only, keep our focus trap.** Viable middle ground, but the
  focus trap is the single highest-value replacement (most code, most subtle), so
  excluding it defeats much of the point. Adopted the integration instead, behind
  a wrapper to contain the blast radius.

# 0015 — Custom selection repaint to suppress the mobile selection toolbar

**Status:** superseded by ADR 0017
**Date:** 2026-06-01

> Superseded by [ADR 0017](0017-fully-custom-touch-selection.md): the "let native
> selection form, then steal it" mechanic below caused a visible selection flash
> and left no way to resize the selection. ADR 0017 owns the touch gesture end to
> end instead. The toolbar-overlap problem and the touch/desktop split it
> identified still stand.

## Context

The reader shows its own tiered selection popover (S4.4) anchored to the user's
text selection. On touch-primary browsers the OS draws a floating selection
toolbar (iOS "Copy / Look Up / Share", Android's action-mode bar) over the same
text. The OS owns that toolbar — there is no web API to hide it while a native
selection exists — and it overlaps our popover.

We support latest Chromium only, which removes the need for WebKit fallbacks and
makes the CSS Custom Highlight API (`CSS.highlights`, `::highlight()`) available.

## Decision

Keep the native selection *gesture* (long-press, word-snap, drag-extend) and the
existing `selectionchange` + debounce detection — both of which survive the OS
selection layer capturing pointer events. On touch-primary devices only
(`matchMedia("(pointer: coarse)")`), once a selection settles we:

1. capture its text and bounding rect (to anchor the popover),
2. repaint the range with the CSS Custom Highlight API so it still reads as
   selected, and
3. call `getSelection().removeAllRanges()`, which collapses the native selection
   and so dismisses the OS toolbar.

A re-entrancy flag swallows the single `selectionchange` our own
`removeAllRanges()` triggers so it isn't read as a dismissal. Desktop (fine
pointer) keeps the native selection untouched — there is no overlapping toolbar
there. The painted highlight's lifecycle is driven by the parent's popover state
(`selectionActive`) plus section change and scroll.

## Consequences

- The mobile OS selection toolbar no longer overlaps our popover.
- On touch, the native drag-handles disappear once the gesture ends, so a user
  adjusts a selection by re-selecting rather than dragging handles. Acceptable
  for a "select a word/phrase to ask about it" reader.
- The highlight pseudo (`::highlight(sm-selection)`) must live in a non-scoped
  style block because the highlight registry is global.
- Touch detection is by primary-pointer media query; a touchscreen laptop driven
  by a mouse reports a fine pointer and keeps native selection (and its toolbar)
  on touch. Not worth handling for our PWA-first, mobile-primary target.

## Alternatives considered

- **Full custom selection** (`user-select: none` + rebuild gesture handling,
  word-snap, and drag-handles from `caretPositionFromPoint`). Total control
  including adjustable handles, but far more code and high risk of edge-case bugs
  (multi-column ranges, RTL). Rejected as disproportionate to the problem.
- **Position the popover to avoid the toolbar.** The OS flips the toolbar's
  placement based on viewport space, so this overlaps near viewport edges.
  Rejected as unreliable.
- **`-webkit-touch-callout: none` alone.** Only suppresses the long-press
  callout, not the post-selection edit toolbar. Kept as a complement, not a fix.

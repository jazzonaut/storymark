# 0017 — Fully custom touch selection (supersedes ADR 0015's mechanics)

**Status:** accepted
**Date:** 2026-06-01

## Context

ADR 0015 suppressed the mobile OS selection toolbar by letting the native
selection form, capturing its range, then calling `removeAllRanges()` and
repainting via the CSS Custom Highlight API. Two problems surfaced in use:

1. **No resizing.** The native drag-handles vanish the moment we clear the
   selection, so a reader is stuck with whatever the long-press first snapped to
   (one word) and cannot extend to a phrase.
2. **A visible flash.** Because the native selection is allowed to form before we
   steal it, the OS paints the selection (and its toolbar) for a beat before it
   disappears — unpolished.

Both are intrinsic to the "let native form, then steal it" approach: the toolbar
and the drag-handles are the same OS selection layer (you cannot keep one without
the other), and the flash is the window in which that layer is briefly live.

We support latest Chromium only, which makes `Intl.Segmenter` (word
segmentation), `document.caretPositionFromPoint` (point → text offset), the CSS
Custom Highlight API, and Pointer Events with capture all available without
fallbacks.

## Decision

On touch-primary devices (`matchMedia("(pointer: coarse)")`) own the entire
selection gesture; native selection never forms (`user-select: none` on the
reader content). Desktop (fine pointer) keeps native selection untouched — there
is no overlapping toolbar there and native selection/copy is better.

The gesture state machine lives in `PaginatedSection.vue`:

- **Long-press** (≈450 ms, cancelled if the finger moves > 10 px first — that's a
  swipe/page) snaps to the word under the point via `Intl.Segmenter` + paints it.
- **Drag while pressing** extends the range toward the finger
  (`caretPositionFromPoint`), repainting live.
- **Lift** settles, shows two custom drag-handles at the range's first/last client
  rects, and emits the selection (text + rect) so the popover anchors.
- **Handle drag** resizes from the opposite edge; the handles set
  `pointer-events: none` mid-drag so `caretPositionFromPoint` reads the text under
  the finger, not the handle, while the captured pointer keeps routing to them.

Scroll-vs-select is arbitrated by a non-passive `touchmove` listener that
`preventDefault()`s while we own the gesture (CSS `touch-action` is latched at
`touchstart`, so a class flip alone is not enough). The painted highlight and
handles are torn down by the parent's popover state (`selectionActive`), section
change, and scroll. The popover gains an explicit **Copy** action because a custom
selection has no native clipboard affordance on touch.

## Consequences

- No native flash and no OS toolbar on touch; selection is fully resizable via
  drag-handles, matching the original intent of the selection popover.
- Selection is naturally **clamped to the current page**: `caretPositionFromPoint`
  only hit-tests visible text, and each multi-column page fills the viewport, so a
  selection cannot span a column/page break. Multiline selection within a page
  works normally.
- Native clipboard is gone on touch; the popover's **Copy** button replaces it.
- Two selection code paths persist (custom on coarse, native on fine). A
  touchscreen laptop driven by a mouse reports a fine pointer and keeps native
  selection — acceptable for our PWA-first, mobile-primary target.
- The handle hit-testing depends on `pointer-events: none` excluding an element
  from `caretPositionFromPoint` while pointer capture still delivers events to it,
  and on the `touch-action`/`preventDefault` interaction. These vary subtly across
  real iOS Safari and Android Chrome and may need device tuning.

## Alternatives considered

- **Keep ADR 0015 and add custom handles only** (reuse native word-snap, rebuild
  just extend-by-handle). Smaller, but leaves the native flash — the specific
  thing this change exists to remove. Rejected.
- **Lengthen the debounce before clearing the native selection** so the user can
  grab the native handles. Cheap, but reintroduces the toolbar overlap during the
  window and keeps the flash. Rejected as a band-aid.
- **Position the popover to dodge the toolbar.** The OS flips toolbar placement by
  available space, so it overlaps near viewport edges. Rejected (carried over from
  ADR 0015).

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
import {
  useDebounceFn,
  useEventListener,
  useMediaQuery,
  useRafFn,
  useResizeObserver,
} from "@vueuse/core";
import type { UseRafFnCallbackArguments } from "@vueuse/core";
import type { Section } from "@storymark/shared-types";
import { useReaderSettings } from "@/composables/use-reader-settings";
import { highlightEntities } from "@/utils/highlight-entities";

// A request to briefly flash a paragraph (e.g. after jumping to a Q&A source).
// `nonce` changes per request so re-tapping the same paragraph re-fires.
interface ParagraphHighlight {
  readonly paragraphIndex: number;
  readonly nonce: number;
}

// A boundary point in the rendered text, used to drive custom selection ranges.
interface Boundary {
  readonly node: Node;
  readonly offset: number;
}

// Viewport placement of a drag-handle, anchored to one edge of the selection.
interface HandlePos {
  readonly x: number;
  readonly top: number;
  readonly height: number;
}

// `entryParagraph` is the paragraph to land on when this section first appears
// (0 = start, last = end, or a resumed position).
const props = defineProps<{
  section: Section;
  entryParagraph: number;
  entryPage: number | null;
  entryRevision: number;
  highlight: ParagraphHighlight | null;
  selectionActive: boolean;
}>();

const emit = defineEmits<{
  (e: "update:paragraph", paragraphIndex: number): void;
  (e: "page-change", page: number, total: number): void;
  (e: "next-section"): void;
  (e: "prev-section"): void;
  (e: "toggle-chrome"): void;
  (
    e: "select",
    payload: { text: string; rect: { top: number; bottom: number; left: number; width: number } },
  ): void;
  (e: "dismiss-select"): void;
  (e: "entity-tap", payload: { entityId: string; name: string }): void;
}>();

const { settings } = useReaderSettings();

// Pre-highlight each paragraph's HTML with tappable entity buttons (keyed by
// paragraph index). Recomputed only when the section or highlight mode changes.
const highlightedHtml = computed<Map<number, string>>(() => {
  const map = new Map<number, string>();
  const mode = settings.entityHighlight;
  for (const paragraph of props.section.paragraphs) {
    if (paragraph.html !== null) {
      map.set(
        paragraph.paragraphIndex,
        highlightEntities(paragraph.html, props.section.entities, mode),
      );
    }
  }
  return map;
});

const SETTLE_MS = 120;
const TAP_EDGE_FRACTION = 0.28;
const FLASH_MS = 2000;
const SELECTION_DEBOUNCE_MS = 250;
const SELECTION_HIGHLIGHT = "sm-selection";
const LONG_PRESS_MS = 450;
const MOVE_CANCEL_PX = 10;
// While dragging a handle the finger sits on the knob, which is drawn off the
// text line; probe this far toward the line so the caret lands on real text.
const HANDLE_PROBE_PX = 18;
// One swipe turns exactly one page (Readera-style page-flip): commit a turn when
// the drag passes this fraction of the page width, or on a short quick flick.
const SWIPE_COMMIT_FRACTION = 0.18;
const FLICK_MAX_MS = 250;
const FLICK_MIN_PX = 24;
// Anchor-capture probe: step down the page in these increments, trying each x
// fraction, until a text caret is found (the top of a page can be padding, an
// image, or a short centred heading, so a single probe point isn't enough).
const ANCHOR_PROBE_STEP_PX = 24;
const ANCHOR_PROBE_X_FRACTIONS = [0.5, 0.25, 0.75];
// Duration of the JS-driven page-turn tween. We animate scrollLeft ourselves (see
// animateTo) rather than via native smooth-scroll, so scroll-snap and the settle
// timer can stay out of the way for the whole turn — otherwise mandatory snap and
// the settle re-scroll fight the in-flight smooth scroll and the flip stutters.
const SWIPE_ANIM_MS = 280;

const scrollEl = ref<HTMLDivElement | null>(null);
const contentEl = ref<HTMLDivElement | null>(null);
const page = ref(0);
const totalPages = ref(1);

// The paragraph at the top of the current page. Tracked locally so a reflow
// (resize / typography change) can scroll the *same* paragraph back into view.
let trackedParagraph = 0;
// The first visible text of the settled page, as a one-character Range. The text
// nodes survive a reflow (only the CSS columns move), so a resize can restore the
// page containing that exact character — paragraph granularity alone yanks the
// reader back to the start of a long paragraph spanning several pages. A section
// payload swap re-renders the DOM and detaches the range; pageForAnchor() detects
// that and restoreToTracked falls back to the paragraph anchor.
let anchorRange: Range | null = null;
let flashTimer: number | undefined;
let flashedEl: HTMLElement | null = null;

// On touch-primary devices the OS selection (and its toolbar/handles) would
// overlap our popover and there is no web API to hide the toolbar while a native
// selection exists. There we suppress native selection entirely (user-select:
// none) and own the whole gesture — long-press to select a word, drag to extend,
// drag the handles to resize — repainting via the CSS Custom Highlight API.
// Desktop (fine pointer) keeps native selection untouched. See ADR 0016.
const coarsePointer = useMediaQuery("(pointer: coarse)");
const wordSegmenter = new Intl.Segmenter(undefined, { granularity: "word" });

// The live custom selection on touch and the fixed boundary the active gesture
// extends away from (the long-pressed word, or the handle that isn't moving).
let customRange: Range | null = null;
let gestureAnchorStart: Boundary | null = null;
let gestureAnchorEnd: Boundary | null = null;
let pressTimer: number | undefined;
let pressPointerId: number | null = null;
let pressStartX = 0;
let pressStartY = 0;
let pressStartTime = 0;
let gestureMode: "idle" | "pressing" | "selecting" | "swiping" = "idle";
let draggingHandle: "start" | "end" | null = null;
// Swipe (page-turn) gesture: the scroll position and page we started the drag
// from, so a quick flick can be measured and snapped relative to the start.
let swipeStartScrollLeft = 0;
let swipeStartPage = 0;
// A drag ends in a synthesised click; this swallows that one click so a swipe
// (or any non-tap drag) never also pages via the edge-tap / chrome-toggle path.
let swallowClick = false;
// A selection was showing when this gesture began. Captured at pointerdown (the
// popover's outside-tap dismisses on the same event, so it's gone by pointermove),
// and used to suppress paging for the whole gesture — a near-miss of a selection
// handle must never turn the page out from under an in-progress selection.
let pressStartedWithSelection = false;
// Locks scroll (touch-action: none) while we own the gesture, so an extend drag
// pans the selection instead of paging.
const scrollLocked = ref(false);
// True while we drive a page-turn swipe ourselves: disables scroll-snap (so the
// page tracks the finger instead of snapping per-increment) and suppresses the
// settle handler (we snap to the chosen page on lift, not to the nearest).
const swiping = ref(false);
// Set during a handle drag: makes the handles ignore pointer hit-testing so
// caretPositionFromPoint reads the text under the finger, not the handle itself.
// The dragged handle keeps its captured pointer regardless.
const handleDragging = ref(false);
const selectionShown = ref(false);
const startHandle = ref<HandlePos | null>(null);
const endHandle = ref<HandlePos | null>(null);
// True while a page-turn tween is running. Like `swiping`, it keeps scroll-snap
// disabled (so the browser doesn't snap mid-tween) and suppresses the settle timer.
const animating = ref(false);

function pageWidth(): number {
  return scrollEl.value?.clientWidth ?? 1;
}

// CSS multi-column needs a fixed column width to lay out one page per viewport;
// pinning it to the live container width keeps pages aligned even when the
// reading area isn't exactly the viewport width.
function measure(): void {
  const scroll = scrollEl.value;
  const content = contentEl.value;
  if (!scroll || !content) {
    return;
  }
  const width = scroll.clientWidth || 1;
  content.style.columnWidth = `${String(width)}px`;
  // Cap inline images to the usable column height (content box, i.e. minus the
  // block padding) so a full-page image — a cover or title plate — fits one page
  // instead of overflowing the column. Set before reading scrollWidth so the
  // clamped image height is what the page count measures. A zero-height pass
  // (hidden ancestor, pre-layout) would produce a negative value that invalidates
  // max-height and disables the cap — keep the last good value instead.
  const styles = getComputedStyle(content);
  const pageHeight =
    content.clientHeight -
    Number.parseFloat(styles.paddingTop) -
    Number.parseFloat(styles.paddingBottom);
  if (pageHeight > 0) {
    content.style.setProperty("--reader-page-height", `${String(pageHeight)}px`);
  }
  totalPages.value = Math.max(1, Math.round(scroll.scrollWidth / width));
}

function anchors(): readonly HTMLElement[] {
  const scroll = scrollEl.value;
  if (!scroll) {
    return [];
  }
  return [...scroll.querySelectorAll<HTMLElement>("[data-paragraph-index]")];
}

function indexOf(anchor: HTMLElement): number {
  const raw = anchor.dataset.paragraphIndex;
  const parsed = raw === undefined ? Number.NaN : Number.parseInt(raw, 10);
  return Number.isNaN(parsed) ? 0 : parsed;
}

function pageForParagraph(paragraphIndex: number): number {
  const scroll = scrollEl.value;
  if (!scroll) {
    return 0;
  }
  const width = pageWidth();
  const left = scroll.getBoundingClientRect().left;
  for (const anchor of anchors()) {
    if (indexOf(anchor) === paragraphIndex) {
      const relative = anchor.getBoundingClientRect().left - left + scroll.scrollLeft;
      return Math.round(relative / width);
    }
  }
  return 0;
}

// The page the anchored character sits on in the *current* layout, or null when
// there is no usable anchor (none captured yet, or its nodes were detached by a
// section payload re-render — a detached range reports an all-zero rect).
function pageForAnchor(): number | null {
  const scroll = scrollEl.value;
  if (anchorRange === null || scroll === null) {
    return null;
  }
  const rect = anchorRange.getBoundingClientRect();
  if (rect.width === 0 && rect.height === 0) {
    return null;
  }
  const relative = rect.left - scroll.getBoundingClientRect().left + scroll.scrollLeft;
  // floor, not round: the anchor was the first *visible* text, so it must end up
  // on the restored page, never one ahead. The epsilon absorbs subpixel rects.
  return Math.floor((relative + 2) / pageWidth());
}

function captureAnchor(): void {
  const scroll = scrollEl.value;
  if (scroll === null) {
    return;
  }
  // An anchor still on the settled page is kept as-is. Re-anchoring to the new
  // page top after every restore would drift backwards one page per reflow cycle
  // (nav bar in/out, fullscreen toggle) whenever page boundaries don't line up.
  if (pageForAnchor() === page.value) {
    return;
  }
  const rect = scroll.getBoundingClientRect();
  for (let y = rect.top + ANCHOR_PROBE_STEP_PX; y < rect.bottom; y += ANCHOR_PROBE_STEP_PX) {
    for (const fraction of ANCHOR_PROBE_X_FRACTIONS) {
      const caret = caretFromPoint(rect.left + rect.width * fraction, y);
      if (caret === null) {
        continue;
      }
      const length = caret.node.textContent?.length ?? 0;
      if (length === 0) {
        continue;
      }
      // Span one character — a collapsed range reports a zero rect after reflow.
      const start = Math.min(caret.offset, length - 1);
      const range = document.createRange();
      range.setStart(caret.node, start);
      range.setEnd(caret.node, start + 1);
      anchorRange = range;
      return;
    }
  }
  anchorRange = null;
}

// The active paragraph is the last one whose column starts at or before the
// current page — a long paragraph spanning pages started earlier but still
// occupies the top.
function reportParagraph(): void {
  const scroll = scrollEl.value;
  if (!scroll) {
    return;
  }
  const width = pageWidth();
  const current = Math.round(scroll.scrollLeft / width);
  const left = scroll.getBoundingClientRect().left;
  let best: number | null = null;
  let bestPage = -1;
  for (const anchor of anchors()) {
    const relative = anchor.getBoundingClientRect().left - left + scroll.scrollLeft;
    const anchorPage = Math.round(relative / width);
    if (anchorPage <= current && anchorPage > bestPage) {
      bestPage = anchorPage;
      best = indexOf(anchor);
    }
  }
  const resolved = best ?? indexOf(anchors()[0] ?? contentEl.value ?? scroll);
  trackedParagraph = resolved;
  emit("update:paragraph", resolved);
  captureAnchor();
}

function updatePageInfo(): void {
  const scroll = scrollEl.value;
  if (!scroll) {
    return;
  }
  page.value = Math.round(scroll.scrollLeft / pageWidth());
  emit("page-change", page.value, totalPages.value);
}

// --- Page-turn animation ----------------------------------------------------

let animFrom = 0;
let animTo = 0;
let animStartTs = 0;

function easeOutCubic(t: number): number {
  return 1 - (1 - t) ** 3;
}

function tickAnimation({ timestamp }: UseRafFnCallbackArguments): void {
  const scroll = scrollEl.value;
  if (scroll === null) {
    finishAnimation();
    return;
  }
  if (animStartTs === 0) {
    animStartTs = timestamp;
  }
  const t = Math.min(1, (timestamp - animStartTs) / SWIPE_ANIM_MS);
  scroll.scrollLeft = animFrom + (animTo - animFrom) * easeOutCubic(t);
  if (t >= 1) {
    finishAnimation();
  }
}

const { pause: pauseAnimation, resume: resumeAnimation } = useRafFn(tickAnimation, {
  immediate: false,
});

function finishAnimation(): void {
  pauseAnimation();
  animStartTs = 0;
  const scroll = scrollEl.value;
  if (scroll !== null) {
    scroll.scrollLeft = animTo;
  }
  animating.value = false;
  updatePageInfo();
  reportParagraph();
}

function animateTo(targetLeft: number): void {
  const scroll = scrollEl.value;
  if (scroll === null) {
    return;
  }
  animFrom = scroll.scrollLeft;
  animTo = targetLeft;
  animStartTs = 0;
  animating.value = true;
  resumeAnimation();
}

function cancelAnimation(): void {
  if (animating.value) {
    pauseAnimation();
    animating.value = false;
    animStartTs = 0;
  }
}

function goToPage(target: number, smooth: boolean): void {
  const scroll = scrollEl.value;
  if (!scroll) {
    return;
  }
  const width = pageWidth();
  const clamped = Math.min(Math.max(target, 0), totalPages.value - 1);
  if (smooth) {
    animateTo(clamped * width);
  } else {
    cancelAnimation();
    scroll.scrollLeft = clamped * width;
  }
}

function nextPage(): void {
  if (page.value >= totalPages.value - 1) {
    emit("next-section");
    return;
  }
  goToPage(page.value + 1, true);
}

function prevPage(): void {
  if (page.value <= 0) {
    emit("prev-section");
    return;
  }
  goToPage(page.value - 1, true);
}

function onSettle(): void {
  const scroll = scrollEl.value;
  if (!scroll) {
    return;
  }
  const width = pageWidth();
  const nearest = Math.round(scroll.scrollLeft / width);
  const target = nearest * width;
  if (Math.abs(scroll.scrollLeft - target) > 1) {
    animateTo(target);
    return;
  }
  updatePageInfo();
  reportParagraph();
}

const debouncedSettle = useDebounceFn(onSettle, SETTLE_MS);

function onScroll(): void {
  // Paging dismisses any open selection popover.
  emit("dismiss-select");
  // During an owned swipe the page tracks the finger, and during our own tween we
  // drive scrollLeft frame by frame; either way endSwipe/finishAnimation settles it.
  if (swiping.value || animating.value) {
    return;
  }
  void debouncedSettle();
}

function paintSelection(range: Range): void {
  CSS.highlights.set(SELECTION_HIGHLIGHT, new Highlight(range.cloneRange()));
}

function clearPaintedSelection(): void {
  CSS.highlights.delete(SELECTION_HIGHLIGHT);
}

// Desktop only: report a settled native selection inside the reader so the
// popover can anchor to it; a collapsed/empty selection dismisses it. On touch
// native selection is suppressed and the gesture handlers below drive selection.
function readSelection(): void {
  const scroll = scrollEl.value;
  if (!scroll) {
    return;
  }
  const selection = document.getSelection();
  if (selection === null || selection.isCollapsed || selection.rangeCount === 0) {
    emit("dismiss-select");
    return;
  }
  const range = selection.getRangeAt(0);
  if (!scroll.contains(range.startContainer) || !scroll.contains(range.endContainer)) {
    return;
  }
  const text = selection.toString().trim();
  if (text.length === 0) {
    emit("dismiss-select");
    return;
  }
  const rect = range.getBoundingClientRect();
  emit("select", {
    text,
    rect: { top: rect.top, bottom: rect.bottom, left: rect.left, width: rect.width },
  });
}

const debouncedReadSelection = useDebounceFn(readSelection, SELECTION_DEBOUNCE_MS);

function onSelectionChange(): void {
  if (coarsePointer.value) {
    return;
  }
  void debouncedReadSelection();
}

function hasActiveSelection(): boolean {
  if (coarsePointer.value) {
    return customRange !== null;
  }
  const selection = document.getSelection();
  return selection !== null && !selection.isCollapsed && selection.toString().trim().length > 0;
}

// --- Custom touch selection -------------------------------------------------

function caretFromPoint(x: number, y: number): Boundary | null {
  const content = contentEl.value;
  if (!content) {
    return null;
  }
  // Chromium 128+ and Firefox have the standard API; WebKit only ships it from
  // Safari 26.2, so older Safari takes the non-standard caretRangeFromPoint path.
  if (typeof document.caretPositionFromPoint === "function") {
    const pos = document.caretPositionFromPoint(x, y);
    if (pos === null || pos.offsetNode.nodeType !== Node.TEXT_NODE) {
      return null;
    }
    if (!content.contains(pos.offsetNode)) {
      return null;
    }
    return { node: pos.offsetNode, offset: pos.offset };
  }
  const range = document.caretRangeFromPoint(x, y);
  if (range === null || range.startContainer.nodeType !== Node.TEXT_NODE) {
    return null;
  }
  if (!content.contains(range.startContainer)) {
    return null;
  }
  return { node: range.startContainer, offset: range.startOffset };
}

// Expand a caret to the word-like segment under it, so a long-press selects a
// whole word the way native selection does.
function wordRangeAt(node: Node, offset: number): Range | null {
  const text = node.textContent ?? "";
  if (text.length === 0) {
    return null;
  }
  const probe = Math.min(offset, text.length - 1);
  for (const segment of wordSegmenter.segment(text)) {
    const end = segment.index + segment.segment.length;
    if (probe >= segment.index && probe < end) {
      const range = document.createRange();
      range.setStart(node, segment.index);
      range.setEnd(node, end);
      return range;
    }
  }
  return null;
}

// Build a range from a fixed anchor boundary to a moving boundary, ordering the
// two so the range is always start-before-end.
function rangeFromAnchor(anchor: Boundary, moving: Boundary): Range {
  const range = document.createRange();
  range.setStart(anchor.node, anchor.offset);
  range.collapse(true);
  if (range.comparePoint(moving.node, moving.offset) >= 0) {
    range.setEnd(moving.node, moving.offset);
  } else {
    range.setStart(moving.node, moving.offset);
    range.setEnd(anchor.node, anchor.offset);
  }
  return range;
}

function positionHandles(): void {
  if (customRange === null) {
    startHandle.value = null;
    endHandle.value = null;
    return;
  }
  const rects = customRange.getClientRects();
  const first = rects.item(0);
  const last = rects.item(rects.length - 1);
  if (first === null || last === null) {
    startHandle.value = null;
    endHandle.value = null;
    return;
  }
  startHandle.value = { x: first.left, top: first.top, height: first.height };
  endHandle.value = { x: last.right, top: last.top, height: last.height };
}

function emitCustomSelection(): void {
  if (customRange === null) {
    return;
  }
  const text = customRange.toString().trim();
  if (text.length === 0) {
    emit("dismiss-select");
    return;
  }
  const rect = customRange.getBoundingClientRect();
  emit("select", {
    text,
    rect: { top: rect.top, bottom: rect.bottom, left: rect.left, width: rect.width },
  });
}

function setCustomRange(range: Range): void {
  customRange = range;
  paintSelection(range);
  positionHandles();
}

function cancelPress(): void {
  if (pressTimer !== undefined) {
    window.clearTimeout(pressTimer);
    pressTimer = undefined;
  }
  pressPointerId = null;
  if (gestureMode === "pressing") {
    gestureMode = "idle";
  }
}

// Drop every visual trace of a custom selection when the popover dismisses
// (outside tap, scroll, "Ask…"). Deliberately leaves an in-flight press alone:
// the dismissing tap may itself be the start of a long-press on new text. Never
// emits.
function clearSelectionVisuals(): void {
  customRange = null;
  gestureAnchorStart = null;
  gestureAnchorEnd = null;
  draggingHandle = null;
  scrollLocked.value = false;
  handleDragging.value = false;
  selectionShown.value = false;
  startHandle.value = null;
  endHandle.value = null;
  clearPaintedSelection();
}

function beginSelection(): void {
  pressTimer = undefined;
  const scroll = scrollEl.value;
  if (scroll === null || pressPointerId === null) {
    return;
  }
  const caret = caretFromPoint(pressStartX, pressStartY);
  const word = caret === null ? null : wordRangeAt(caret.node, caret.offset);
  if (word === null) {
    gestureMode = "idle";
    return;
  }
  gestureAnchorStart = { node: word.startContainer, offset: word.startOffset };
  gestureAnchorEnd = { node: word.endContainer, offset: word.endOffset };
  gestureMode = "selecting";
  scrollLocked.value = true;
  scroll.setPointerCapture(pressPointerId);
  setCustomRange(word);
}

// Extend the initial word-anchored selection toward the dragging finger; the
// caret hit-tests only visible text, so the range stays on the current page.
function extendSelection(x: number, y: number): void {
  if (gestureAnchorStart === null || gestureAnchorEnd === null) {
    return;
  }
  const caret = caretFromPoint(x, y);
  if (caret === null) {
    return;
  }
  const base = document.createRange();
  base.setStart(gestureAnchorStart.node, gestureAnchorStart.offset);
  base.setEnd(gestureAnchorEnd.node, gestureAnchorEnd.offset);
  const side = base.comparePoint(caret.node, caret.offset);
  if (side > 0) {
    base.setEnd(caret.node, caret.offset);
  } else if (side < 0) {
    base.setStart(caret.node, caret.offset);
  }
  setCustomRange(base);
}

// --- Page-turn swipe (touch) ------------------------------------------------

// Take over the gesture as a page-turn drag: the finger now pans the columns and
// the page snaps to its destination on lift (see endSwipe).
function beginSwipe(): void {
  const scroll = scrollEl.value;
  if (scroll === null || pressPointerId === null) {
    gestureMode = "idle";
    return;
  }
  // A drag mid-tween takes over from wherever the page currently sits, so stop the
  // tween first (otherwise it keeps writing scrollLeft and fights the finger).
  cancelAnimation();
  gestureMode = "swiping";
  swiping.value = true;
  swipeStartScrollLeft = scroll.scrollLeft;
  swipeStartPage = page.value;
  scroll.setPointerCapture(pressPointerId);
}

// The page follows the finger 1:1, but never past the adjacent page in either
// direction (nor the section's scroll range) — one swipe only ever reveals one
// page, so it can never run on into the rest of the section.
function dragSwipe(clientX: number): void {
  const scroll = scrollEl.value;
  if (scroll === null) {
    return;
  }
  const width = pageWidth();
  const max = Math.max(0, (totalPages.value - 1) * width);
  const lower = Math.max(0, swipeStartScrollLeft - width);
  const upper = Math.min(max, swipeStartScrollLeft + width);
  const target = swipeStartScrollLeft - (clientX - pressStartX);
  scroll.scrollLeft = Math.min(Math.max(target, lower), upper);
}

// Settle the swipe: commit a single page turn when the drag crossed the distance
// threshold or was a short quick flick; otherwise snap back to where it started.
function endSwipe(clientX: number, time: number): void {
  const scroll = scrollEl.value;
  if (scroll !== null && pressPointerId !== null && scroll.hasPointerCapture(pressPointerId)) {
    scroll.releasePointerCapture(pressPointerId);
  }
  gestureMode = "idle";
  pressPointerId = null;
  swiping.value = false;
  const dx = clientX - pressStartX;
  const commit =
    Math.abs(dx) >= pageWidth() * SWIPE_COMMIT_FRACTION ||
    (Math.abs(dx) >= FLICK_MIN_PX && time - pressStartTime <= FLICK_MAX_MS);
  if (commit && dx < 0) {
    nextPage();
  } else if (commit && dx > 0) {
    prevPage();
  } else {
    goToPage(swipeStartPage, true);
  }
}

function onPointerDown(event: PointerEvent): void {
  if (!coarsePointer.value || draggingHandle !== null || !event.isPrimary) {
    return;
  }
  cancelPress();
  swallowClick = false;
  pressStartedWithSelection = hasActiveSelection();
  pressPointerId = event.pointerId;
  pressStartX = event.clientX;
  pressStartY = event.clientY;
  pressStartTime = event.timeStamp;
  gestureMode = "pressing";
  pressTimer = window.setTimeout(beginSelection, LONG_PRESS_MS);
}

function onPointerMove(event: PointerEvent): void {
  if (!coarsePointer.value) {
    return;
  }
  if (gestureMode === "pressing") {
    const dx = event.clientX - pressStartX;
    const dy = event.clientY - pressStartY;
    if (Math.hypot(dx, dy) > MOVE_CANCEL_PX) {
      // The finger moved before the long-press fired: this is a drag, not a tap
      // or a word-select, so cancel both the press and its trailing click. A
      // horizontal drag turns the page; a vertical one has nothing to scroll.
      if (pressTimer !== undefined) {
        window.clearTimeout(pressTimer);
        pressTimer = undefined;
      }
      swallowClick = true;
      // A horizontal drag pages — but not while a selection is up: that gesture is
      // a missed handle grab, so let the popover dismiss it without also paging.
      if (Math.abs(dx) > Math.abs(dy) && !pressStartedWithSelection) {
        beginSwipe();
        dragSwipe(event.clientX);
      } else {
        gestureMode = "idle";
        pressPointerId = null;
      }
    }
    return;
  }
  if (gestureMode === "swiping") {
    dragSwipe(event.clientX);
    return;
  }
  if (gestureMode === "selecting") {
    extendSelection(event.clientX, event.clientY);
  }
}

function onPointerUp(event: PointerEvent): void {
  if (!coarsePointer.value) {
    return;
  }
  if (gestureMode === "pressing") {
    cancelPress();
    return;
  }
  if (gestureMode === "swiping") {
    endSwipe(event.clientX, event.timeStamp);
    return;
  }
  if (gestureMode === "selecting") {
    const scroll = scrollEl.value;
    if (scroll !== null && pressPointerId !== null && scroll.hasPointerCapture(pressPointerId)) {
      scroll.releasePointerCapture(pressPointerId);
    }
    gestureMode = "idle";
    pressPointerId = null;
    scrollLocked.value = false;
    selectionShown.value = customRange !== null;
    positionHandles();
    emitCustomSelection();
  }
}

function onPointerCancel(): void {
  if (!coarsePointer.value) {
    return;
  }
  if (gestureMode === "swiping") {
    gestureMode = "idle";
    pressPointerId = null;
    swiping.value = false;
    goToPage(swipeStartPage, true);
    return;
  }
  if (gestureMode === "selecting") {
    emit("dismiss-select");
  }
  cancelPress();
}

function onHandleDown(which: "start" | "end", event: PointerEvent): void {
  if (customRange === null) {
    return;
  }
  event.stopPropagation();
  event.preventDefault();
  draggingHandle = which;
  scrollLocked.value = true;
  handleDragging.value = true;
  // Anchor on the opposite edge so dragging this handle resizes from there.
  gestureAnchorStart =
    which === "start"
      ? { node: customRange.endContainer, offset: customRange.endOffset }
      : { node: customRange.startContainer, offset: customRange.startOffset };
  const target = event.currentTarget;
  if (target instanceof Element) {
    target.setPointerCapture(event.pointerId);
  }
}

function onHandleMove(event: PointerEvent): void {
  if (draggingHandle === null || gestureAnchorStart === null) {
    return;
  }
  const probeY =
    draggingHandle === "start" ? event.clientY + HANDLE_PROBE_PX : event.clientY - HANDLE_PROBE_PX;
  const caret = caretFromPoint(event.clientX, probeY);
  if (caret === null) {
    return;
  }
  const next = rangeFromAnchor(gestureAnchorStart, caret);
  if (next.collapsed) {
    return;
  }
  setCustomRange(next);
}

function onHandleUp(): void {
  if (draggingHandle === null) {
    return;
  }
  draggingHandle = null;
  gestureAnchorStart = null;
  scrollLocked.value = false;
  handleDragging.value = false;
  emitCustomSelection();
}

function onClick(event: MouseEvent): void {
  // A swipe (or any drag) ends in a click; consume that one so it doesn't also
  // page or toggle chrome via the edge-tap path below.
  if (swallowClick) {
    swallowClick = false;
    return;
  }
  // Tapping a highlighted entity opens its popup; never pages or toggles chrome.
  const entityEl =
    event.target instanceof Element ? event.target.closest<HTMLElement>("[data-entity-id]") : null;
  if (entityEl !== null) {
    event.preventDefault();
    const entityId = entityEl.dataset.entityId;
    if (entityId !== undefined && entityId.length > 0) {
      emit("entity-tap", { entityId, name: entityEl.dataset.entityName ?? "" });
    }
    return;
  }
  // Internal links in book HTML are relative/fragment hrefs; navigating them
  // would break the SPA. Swallow the click (in-book link routing is future work)
  // and don't treat it as a page tap.
  if (event.target instanceof Element && event.target.closest("a")) {
    event.preventDefault();
    return;
  }
  // A drag-select ends in a click; don't let it turn the page or toggle chrome.
  // Desktop checks the native selection; touch checks our custom range. Also
  // swallow the tap that dismisses an open selection so it never also pages.
  if (hasActiveSelection() || pressStartedWithSelection) {
    return;
  }
  const scroll = scrollEl.value;
  if (!scroll) {
    return;
  }
  const x = event.clientX - scroll.getBoundingClientRect().left;
  const width = scroll.clientWidth;
  if (x < width * TAP_EDGE_FRACTION) {
    prevPage();
  } else if (x > width * (1 - TAP_EDGE_FRACTION)) {
    nextPage();
  } else {
    emit("toggle-chrome");
  }
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === "ArrowRight" || event.key === "PageDown") {
    event.preventDefault();
    nextPage();
  } else if (event.key === "ArrowLeft" || event.key === "PageUp") {
    event.preventDefault();
    prevPage();
  }
}

// touch-action flips to none via the scroll-lock class once we own the gesture,
// but the browser latches touch-action at touchstart, so cancel the in-flight
// scroll for the gesture's own moves with a non-passive listener.
function onTouchMove(event: TouchEvent): void {
  if (gestureMode === "selecting" || gestureMode === "swiping" || draggingHandle !== null) {
    event.preventDefault();
  }
}

// Land on the section's entry point by default (a fresh mount / navigation). Pass a
// paragraph to anchor there instead — used when the section payload is swapped in place
// (an entity merge re-fetched the same section), where the reader must not be yanked back
// to the entry point.
async function reposition(toParagraph?: number): Promise<void> {
  await nextTick();
  measure();
  goToPage(
    toParagraph !== undefined
      ? pageForParagraph(toParagraph)
      : (props.entryPage ?? pageForParagraph(props.entryParagraph)),
    false,
  );
  updatePageInfo();
  reportParagraph();
}

function restoreToTracked(): void {
  measure();
  goToPage(pageForAnchor() ?? pageForParagraph(trackedParagraph), false);
  updatePageInfo();
}

function clearFlash(): void {
  if (flashTimer !== undefined) {
    window.clearTimeout(flashTimer);
    flashTimer = undefined;
  }
  flashedEl?.classList.remove("reader-para--flash");
  flashedEl = null;
}

function flashParagraph(paragraphIndex: number): void {
  clearFlash();
  const el = scrollEl.value?.querySelector<HTMLElement>(
    `[data-paragraph-index="${String(paragraphIndex)}"]`,
  );
  if (!el) {
    return;
  }
  el.classList.add("reader-para--flash");
  flashedEl = el;
  flashTimer = window.setTimeout(clearFlash, FLASH_MS);
}

// props.section changes without a remount in two same-section cases (a *cross*-section
// navigation re-keys the wrapper, so onMounted handles it): an in-place payload refresh
// (entity merge) and a same-section jump (bookmark, Q&A source, "back to reading").
// They need opposite landings — stay on the tracked paragraph vs. go to the new entry
// point — and only the navigation bumps entryRevision, so that's the discriminator.
watch(
  () => [props.section, props.entryRevision] as const,
  ([, revision], [, previousRevision]) => {
    cancelPress();
    clearSelectionVisuals();
    void reposition(revision === previousRevision ? trackedParagraph : undefined);
  },
  { flush: "post" },
);

// The popover lives in the parent; when it dismisses (outside tap, "Ask…", etc.)
// drop our repainted selection and handles too.
watch(
  () => props.selectionActive,
  (active) => {
    if (!active) {
      clearSelectionVisuals();
    }
  },
);

// A new highlight object (incl. nonce) means "flash this paragraph now". Post
// flush so the section-change reposition has rendered the target paragraph.
watch(
  () => props.highlight,
  (highlight) => {
    if (highlight === null) {
      return;
    }
    void nextTick(() => {
      flashParagraph(highlight.paragraphIndex);
    });
  },
  { flush: "post" },
);

// Observe both: contentEl catches typography reflow (column-count changes), scrollEl
// catches viewport resize (which leaves the JS-pinned columnWidth stale until we
// re-measure). Either way, keep the anchored text in view.
useResizeObserver(contentEl, restoreToTracked);
useResizeObserver(scrollEl, restoreToTracked);

useEventListener(window, "keydown", onKeydown);
useEventListener(document, "selectionchange", onSelectionChange);
// touch-action is latched at touchstart, so flipping the scroll-lock class alone
// can't stop the page paging mid-gesture; cancel the in-flight scroll with a
// non-passive listener. Only coarse pointers own the gesture, so only they need it.
if (coarsePointer.value) {
  useEventListener(scrollEl, "touchmove", onTouchMove, { passive: false });
}

onMounted(() => {
  void reposition();
});

onBeforeUnmount(() => {
  if (pressTimer !== undefined) {
    window.clearTimeout(pressTimer);
  }
  clearPaintedSelection();
  clearFlash();
});
</script>

<template>
  <div
    ref="scrollEl"
    class="reader-scroll"
    :class="{
      'reader-scroll--coarse': coarsePointer,
      'reader-scroll--locked': scrollLocked,
      'reader-scroll--swiping': swiping || animating,
    }"
    @scroll="onScroll"
    @click="onClick"
    @pointerdown="onPointerDown"
    @pointermove="onPointerMove"
    @pointerup="onPointerUp"
    @pointercancel="onPointerCancel"
  >
    <div
      ref="contentEl"
      class="reader-content"
      :class="`reader-content--hl-${settings.entityHighlight}`"
    >
      <!-- Paragraph HTML is sanitised server-side by ContentSanitizationService
           (no script/on*/inline-style/external refs); rendering it is safe per
           docs/conventions/02-vue.md. The [data-paragraph-index] wrapper is the
           position anchor used for reflow-stable pagination. -->
      <div
        v-for="paragraph in section.paragraphs"
        :key="paragraph.paragraphIndex"
        class="reader-para"
        :data-paragraph-index="paragraph.paragraphIndex"
      >
        <div
          v-if="paragraph.html"
          v-html="highlightedHtml.get(paragraph.paragraphIndex) ?? paragraph.html"
        ></div>
        <p v-else>{{ paragraph.text }}</p>
      </div>
    </div>
  </div>

  <!-- Custom selection handles (touch only). Teleported so position:fixed isn't
       clipped by the scroll container and they survive any ancestor transform. -->
  <Teleport to="body">
    <template v-if="selectionShown">
      <div
        v-if="startHandle"
        class="reader-sel-handle reader-sel-handle--start"
        :class="{ 'reader-sel-handle--inert': handleDragging }"
        data-sm-selection-handle
        :style="{
          left: `${String(startHandle.x)}px`,
          top: `${String(startHandle.top)}px`,
          height: `${String(startHandle.height)}px`,
        }"
        @pointerdown="onHandleDown('start', $event)"
        @pointermove="onHandleMove"
        @pointerup="onHandleUp"
        @pointercancel="onHandleUp"
      ></div>
      <div
        v-if="endHandle"
        class="reader-sel-handle reader-sel-handle--end"
        :class="{ 'reader-sel-handle--inert': handleDragging }"
        data-sm-selection-handle
        :style="{
          left: `${String(endHandle.x)}px`,
          top: `${String(endHandle.top)}px`,
          height: `${String(endHandle.height)}px`,
        }"
        @pointerdown="onHandleDown('end', $event)"
        @pointermove="onHandleMove"
        @pointerup="onHandleUp"
        @pointercancel="onHandleUp"
      ></div>
    </template>
  </Teleport>
</template>

<style scoped>
.reader-scroll {
  height: 100%;
  overflow-x: auto;
  overflow-y: hidden;
  scroll-snap-type: x mandatory;
  -webkit-overflow-scrolling: touch;
  touch-action: pan-x;
  scrollbar-width: none;
  cursor: pointer;
}

/* Touch: own selection ourselves, so suppress the native selection and its
   long-press callout entirely (see the gesture handlers / ADR 0016). */
.reader-scroll--coarse .reader-content {
  user-select: none;
  -webkit-user-select: none;
  -webkit-touch-callout: none;
}

/* Touch: own paging and selection ourselves. Native momentum scrolling flings
   across several pages before snapping; instead we drive a one-page-per-swipe
   drag from the pointer handlers (see beginSwipe / endSwipe). */
.reader-scroll--coarse {
  touch-action: none;
}

/* While we own the gesture, stop the page from paging under the drag. */
.reader-scroll--locked {
  touch-action: none;
}

/* The page tracks the finger during a swipe, so mandatory snap (which would jump
   it per page-increment) is disabled until the drag settles. */
.reader-scroll--swiping {
  scroll-snap-type: none;
}

.reader-scroll::-webkit-scrollbar {
  display: none;
}

.reader-content {
  height: 100%;
  column-width: 100vw;
  column-gap: 0;
  column-fill: auto;
  padding-block: 3.75rem 2.75rem;
  box-sizing: border-box;
  font-family: var(--reader-font-family);
  font-size: var(--reader-font-size);
  line-height: var(--reader-line-height);
}

.reader-para {
  max-width: var(--reader-content-width);
  margin-inline: auto;
  padding-inline: var(--reader-page-margin);
}

.reader-para + .reader-para {
  margin-top: var(--reader-paragraph-spacing);
}

/* One-shot fade after jumping to a Q&A source, so the eye lands on it. */
.reader-para--flash {
  border-radius: 0.375rem;
  animation: reader-para-flash 2s ease-out;
}

@keyframes reader-para-flash {
  from {
    background-color: color-mix(in srgb, var(--sm-accent) 26%, transparent);
  }
  to {
    background-color: transparent;
  }
}

.reader-para :deep(img) {
  max-width: 100%;
  max-height: var(--reader-page-height, 100dvh);
  height: auto;
}

/* Scene-break rule: a short centred hairline rather than the full-width UA
   default, which reads as a chapter boundary instead of a beat. */
.reader-para :deep(hr) {
  width: 40%;
  margin-inline: auto;
  border: none;
  border-top: 1px solid currentColor;
  opacity: 0.35;
}

.reader-para :deep(p) {
  margin: 0;
}

/* Highlighted entity hits are injected via v-html, so reach them with :deep.
   Reset the <button> to flow as inline text, then mark it by highlight mode. */
.reader-content :deep(.sm-entity) {
  display: inline;
  margin: 0;
  padding: 0;
  border: none;
  background: none;
  font: inherit;
  color: inherit;
  text-align: inherit;
  cursor: pointer;
}

.reader-content--hl-default :deep(.sm-entity),
.reader-content--hl-subtle :deep(.sm-entity) {
  text-decoration: underline dotted;
  text-underline-offset: 0.18em;
}

.reader-content--hl-default :deep(.sm-entity) {
  text-decoration-color: color-mix(in srgb, var(--sm-accent) 50%, transparent);
}

.reader-content--hl-subtle :deep(.sm-entity) {
  text-decoration-color: color-mix(in srgb, var(--sm-accent) 22%, transparent);
}
</style>

<!-- Teleported handles live at the body, so their styles aren't scoped. -->
<style>
.reader-sel-handle {
  position: fixed;
  z-index: 50;
  width: 2px;
  background-color: var(--sm-accent);
  touch-action: none;
}

/* The visible knob, drawn just off the thin bar. */
.reader-sel-handle::before {
  content: "";
  position: absolute;
  left: 50%;
  width: 20px;
  height: 20px;
  border-radius: 9999px;
  background-color: var(--sm-accent);
  transform: translateX(-50%);
}

/* A finger-sized transparent grab target around the knob. */
.reader-sel-handle::after {
  content: "";
  position: absolute;
  left: 50%;
  width: 3.5rem;
  height: 3.5rem;
  transform: translateX(-50%);
}

.reader-sel-handle--start::before {
  top: -19px;
}

.reader-sel-handle--start::after {
  top: -42px;
}

.reader-sel-handle--end::before {
  bottom: -19px;
}

.reader-sel-handle--end::after {
  bottom: -42px;
}

/* During a drag the handle (and its grab target) must not intercept
   caretPositionFromPoint; the captured pointer still routes here. */
.reader-sel-handle--inert {
  pointer-events: none;
}

::highlight(sm-selection) {
  background-color: color-mix(in srgb, var(--sm-accent) 30%, transparent);
}
</style>

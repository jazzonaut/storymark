<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
import type { Section } from "@storymark/shared-types";

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
}>();

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

const scrollEl = ref<HTMLDivElement | null>(null);
const contentEl = ref<HTMLDivElement | null>(null);
const page = ref(0);
const totalPages = ref(1);

// The paragraph at the top of the current page. Tracked locally so a reflow
// (resize / typography change) can scroll the *same* paragraph back into view.
let trackedParagraph = 0;
let settleTimer: number | undefined;
let resizeObserver: ResizeObserver | undefined;
let flashTimer: number | undefined;
let flashedEl: HTMLElement | null = null;
let selectionTimer: number | undefined;

// On touch-primary devices the OS selection (and its toolbar/handles) would
// overlap our popover and there is no web API to hide the toolbar while a native
// selection exists. There we suppress native selection entirely (user-select:
// none) and own the whole gesture — long-press to select a word, drag to extend,
// drag the handles to resize — repainting via the CSS Custom Highlight API.
// Desktop (fine pointer) keeps native selection untouched. See ADR 0016.
const coarsePointer = window.matchMedia("(pointer: coarse)").matches;
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
let gestureMode: "idle" | "pressing" | "selecting" = "idle";
let draggingHandle: "start" | "end" | null = null;
// Locks scroll (touch-action: none) while we own the gesture, so an extend drag
// pans the selection instead of paging.
const scrollLocked = ref(false);
// Set during a handle drag: makes the handles ignore pointer hit-testing so
// caretPositionFromPoint reads the text under the finger, not the handle itself.
// The dragged handle keeps its captured pointer regardless.
const handleDragging = ref(false);
const selectionShown = ref(false);
const startHandle = ref<HandlePos | null>(null);
const endHandle = ref<HandlePos | null>(null);

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
}

function updatePageInfo(): void {
  const scroll = scrollEl.value;
  if (!scroll) {
    return;
  }
  page.value = Math.round(scroll.scrollLeft / pageWidth());
  emit("page-change", page.value, totalPages.value);
}

function goToPage(target: number, smooth: boolean): void {
  const scroll = scrollEl.value;
  if (!scroll) {
    return;
  }
  const width = pageWidth();
  const clamped = Math.min(Math.max(target, 0), totalPages.value - 1);
  scroll.scrollTo({ left: clamped * width, behavior: smooth ? "smooth" : "auto" });
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
    scroll.scrollTo({ left: target, behavior: "smooth" });
    return;
  }
  updatePageInfo();
  reportParagraph();
}

function onScroll(): void {
  // Paging dismisses any open selection popover.
  emit("dismiss-select");
  if (settleTimer !== undefined) {
    window.clearTimeout(settleTimer);
  }
  settleTimer = window.setTimeout(onSettle, SETTLE_MS);
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

function onSelectionChange(): void {
  if (coarsePointer) {
    return;
  }
  if (selectionTimer !== undefined) {
    window.clearTimeout(selectionTimer);
  }
  selectionTimer = window.setTimeout(readSelection, SELECTION_DEBOUNCE_MS);
}

function hasActiveSelection(): boolean {
  if (coarsePointer) {
    return customRange !== null;
  }
  const selection = document.getSelection();
  return selection !== null && !selection.isCollapsed && selection.toString().trim().length > 0;
}

// --- Custom touch selection -------------------------------------------------

function caretFromPoint(x: number, y: number): Boundary | null {
  const pos = document.caretPositionFromPoint(x, y);
  if (pos === null || pos.offsetNode.nodeType !== Node.TEXT_NODE) {
    return null;
  }
  const content = contentEl.value;
  if (!content || !content.contains(pos.offsetNode)) {
    return null;
  }
  return { node: pos.offsetNode, offset: pos.offset };
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

function onPointerDown(event: PointerEvent): void {
  if (!coarsePointer || draggingHandle !== null || !event.isPrimary) {
    return;
  }
  cancelPress();
  pressPointerId = event.pointerId;
  pressStartX = event.clientX;
  pressStartY = event.clientY;
  gestureMode = "pressing";
  pressTimer = window.setTimeout(beginSelection, LONG_PRESS_MS);
}

function onPointerMove(event: PointerEvent): void {
  if (!coarsePointer) {
    return;
  }
  if (gestureMode === "pressing") {
    if (Math.hypot(event.clientX - pressStartX, event.clientY - pressStartY) > MOVE_CANCEL_PX) {
      cancelPress();
    }
    return;
  }
  if (gestureMode === "selecting") {
    extendSelection(event.clientX, event.clientY);
  }
}

function onPointerUp(): void {
  if (!coarsePointer) {
    return;
  }
  if (gestureMode === "pressing") {
    cancelPress();
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
  if (!coarsePointer) {
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
  // Internal links in book HTML are relative/fragment hrefs; navigating them
  // would break the SPA. Swallow the click (in-book link routing is future work)
  // and don't treat it as a page tap.
  if (event.target instanceof Element && event.target.closest("a")) {
    event.preventDefault();
    return;
  }
  // A drag-select ends in a click; don't let it turn the page or toggle chrome.
  // Desktop checks the native selection; touch checks our custom range.
  if (hasActiveSelection()) {
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
  if (gestureMode === "selecting" || draggingHandle !== null) {
    event.preventDefault();
  }
}

async function reposition(): Promise<void> {
  await nextTick();
  measure();
  goToPage(pageForParagraph(props.entryParagraph), false);
  updatePageInfo();
  reportParagraph();
}

function restoreToTracked(): void {
  measure();
  goToPage(pageForParagraph(trackedParagraph), false);
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

watch(
  () => props.section,
  () => {
    cancelPress();
    clearSelectionVisuals();
    void reposition();
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

onMounted(() => {
  void reposition();
  window.addEventListener("keydown", onKeydown);
  document.addEventListener("selectionchange", onSelectionChange);
  // Observe both: contentEl catches typography reflow (column count changes),
  // scrollEl catches viewport resize (which leaves the JS-pinned columnWidth
  // stale until we re-measure). Either way, keep the tracked paragraph in view.
  resizeObserver = new ResizeObserver(() => {
    restoreToTracked();
  });
  const content = contentEl.value;
  const scroll = scrollEl.value;
  if (content) {
    resizeObserver.observe(content);
  }
  if (scroll) {
    resizeObserver.observe(scroll);
    if (coarsePointer) {
      scroll.addEventListener("touchmove", onTouchMove, { passive: false });
    }
  }
});

onBeforeUnmount(() => {
  window.removeEventListener("keydown", onKeydown);
  document.removeEventListener("selectionchange", onSelectionChange);
  scrollEl.value?.removeEventListener("touchmove", onTouchMove);
  resizeObserver?.disconnect();
  if (settleTimer !== undefined) {
    window.clearTimeout(settleTimer);
  }
  if (selectionTimer !== undefined) {
    window.clearTimeout(selectionTimer);
  }
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
    :class="{ 'reader-scroll--coarse': coarsePointer, 'reader-scroll--locked': scrollLocked }"
    @scroll="onScroll"
    @click="onClick"
    @pointerdown="onPointerDown"
    @pointermove="onPointerMove"
    @pointerup="onPointerUp"
    @pointercancel="onPointerCancel"
  >
    <div ref="contentEl" class="reader-content">
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
        <div v-if="paragraph.html" v-html="paragraph.html"></div>
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

/* While we own the gesture, stop the page from paging under the drag. */
.reader-scroll--locked {
  touch-action: none;
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
  padding-inline: 1.5rem;
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
  height: auto;
}

.reader-para :deep(p) {
  margin: 0;
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
  width: 16px;
  height: 16px;
  border-radius: 9999px;
  background-color: var(--sm-accent);
  transform: translateX(-50%);
}

/* A finger-sized transparent grab target around the knob. */
.reader-sel-handle::after {
  content: "";
  position: absolute;
  left: 50%;
  width: 2.75rem;
  height: 2.75rem;
  transform: translateX(-50%);
}

.reader-sel-handle--start::before {
  top: -15px;
}

.reader-sel-handle--start::after {
  top: -36px;
}

.reader-sel-handle--end::before {
  bottom: -15px;
}

.reader-sel-handle--end::after {
  bottom: -36px;
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

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
import type { Section } from "@storymark/shared-types";

// A request to briefly flash a paragraph (e.g. after jumping to a Q&A source).
// `nonce` changes per request so re-tapping the same paragraph re-fires.
interface ParagraphHighlight {
  readonly paragraphIndex: number;
  readonly nonce: number;
}

// `entryParagraph` is the paragraph to land on when this section first appears
// (0 = start, last = end, or a resumed position).
const props = defineProps<{
  section: Section;
  entryParagraph: number;
  highlight: ParagraphHighlight | null;
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

// Report a settled, non-collapsed selection inside the reader so the popover can
// anchor to it; a collapsed/empty selection dismisses it. Covers mouse drag and
// touch long-press without a page-turn (see the guard in onClick).
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
  if (selectionTimer !== undefined) {
    window.clearTimeout(selectionTimer);
  }
  selectionTimer = window.setTimeout(readSelection, SELECTION_DEBOUNCE_MS);
}

function hasActiveSelection(): boolean {
  const selection = document.getSelection();
  return selection !== null && !selection.isCollapsed && selection.toString().trim().length > 0;
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
    void reposition();
  },
  { flush: "post" },
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
  }
});

onBeforeUnmount(() => {
  window.removeEventListener("keydown", onKeydown);
  document.removeEventListener("selectionchange", onSelectionChange);
  resizeObserver?.disconnect();
  if (settleTimer !== undefined) {
    window.clearTimeout(settleTimer);
  }
  if (selectionTimer !== undefined) {
    window.clearTimeout(selectionTimer);
  }
  clearFlash();
});
</script>

<template>
  <div ref="scrollEl" class="reader-scroll" @scroll="onScroll" @click="onClick">
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

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useEventListener, useTimeoutFn, useWakeLock } from "@vueuse/core";
import { useRoute } from "vue-router";
import type { AssistantSource } from "@storymark/shared-types";
import BookmarksPanel from "@/components/reader/BookmarksPanel.vue";
import ContentsPanel from "@/components/reader/ContentsPanel.vue";
import EntityPopup from "@/components/reader/EntityPopup.vue";
import MemoryPanel from "@/components/reader/MemoryPanel.vue";
import PaginatedSection from "@/components/reader/PaginatedSection.vue";
import ReaderSettingsDrawer from "@/components/reader/ReaderSettingsDrawer.vue";
import ReaderToolbar from "@/components/reader/ReaderToolbar.vue";
import SectionRecapCard from "@/components/reader/SectionRecapCard.vue";
import SelectionPopover from "@/components/reader/SelectionPopover.vue";
import { useAssistant } from "@/composables/use-assistant";
import { useEntitySummary } from "@/composables/use-entity-summary";
import { useMemoryPanel } from "@/composables/use-memory-panel";
import type { ReaderSelection } from "@/composables/use-selection-hint";
import { useBookCache } from "@/composables/use-book-cache";
import { useBookmarks } from "@/composables/use-bookmarks";
import { useBooks } from "@/composables/use-books";
import { useOfflineSync } from "@/composables/use-offline-sync";
import { useReader } from "@/composables/use-reader";
import { useReaderSettings } from "@/composables/use-reader-settings";
import { usePreviously } from "@/composables/use-previously";
import { useReadingProgress } from "@/composables/use-reading-progress";
import { useSectionRecap } from "@/composables/use-section-recap";
import { sectionLabel } from "@/utils/section-label";

const CHROME_IDLE_MS = 3500;

const route = useRoute();
const bookId = String(route.params.id);

const { books, loadBooks } = useBooks();
const { isOnline } = useOfflineSync();
const { cssVars, load: loadSettings } = useReaderSettings();
const { progress, ensureCached } = useBookCache();
const {
  manifest,
  currentSection,
  currentPosition,
  currentSectionIndex,
  currentParagraphIndex,
  entryParagraph,
  currentSectionTitle,
  nextSection,
  prevSection,
  setParagraph,
  entryPage,
  open,
  goTo,
  goToLocation,
} = useReader();
const { record, resume, flush } = useReadingProgress();
const { bookmarks, load: loadBookmarks, toggle, removeById, isBookmarked } = useBookmarks();
const { draft: assistantDraft, reset: resetAssistant } = useAssistant();
const { open: openEntitySummary, close: closeEntitySummary } = useEntitySummary();
const {
  restore: restoreMemoryTab,
  setTab: setMemoryTab,
  selectByIndex: selectMemoryTab,
  tabCount: memoryTabCount,
} = useMemoryPanel();
const {
  snapshot: recapSnapshot,
  visible: recapVisible,
  reset: resetRecap,
  onSectionEntered: enterSection,
  dismiss: dismissRecap,
  dontShowAgain: recapDontShowAgain,
} = useSectionRecap();
const {
  snapshot: previouslySnapshot,
  visible: previouslyVisible,
  maybeShow: maybeShowPreviously,
  dismiss: dismissPreviously,
  dontShowAgain: previouslyDontShowAgain,
} = usePreviously();

// Hold a screen wake lock while a book is open so the phone doesn't dim/sleep
// mid-page. VueUse re-acquires it on visibilitychange, so backgrounding the PWA
// and returning keeps the screen awake.
const { request: requestWakeLock, release: releaseWakeLock } = useWakeLock();

const phase = ref<"loading" | "ready" | "error">("loading");
const chromeVisible = ref(true);
const settingsOpen = ref(false);
const bookmarksOpen = ref(false);
const contentsOpen = ref(false);
const memoryPanelOpen = ref(false);
const selection = ref<ReaderSelection | null>(null);
const highlight = ref<{ paragraphIndex: number; nonce: number } | null>(null);
let highlightNonce = 0;
// Flash a paragraph to re-orient the reader after a jump. The nonce bump makes
// re-flashing the same paragraph still re-trigger the child's watch.
function flashParagraph(paragraphIndex: number): void {
  highlightNonce += 1;
  highlight.value = { paragraphIndex, nonce: highlightNonce };
}
// Where the reader was before following a Q&A source, so they can jump back.
// Captured on the first source jump; persists across paging and chapter changes,
// and is cleared only when the reader returns via it or dismisses it (the X).
const returnPoint = ref<{ sectionIndex: number; paragraphIndex: number } | null>(null);
const pageInfo = ref<{ page: number; total: number }>({ page: 0, total: 1 });

// Slide the reader horizontally on every section change instead of snapping: a
// forward move (swipe or jump) slides the new chapter in from the right, a
// backward one from the left, so a chapter boundary turns like a page. The new
// section is keyed/remounted under a <Transition>; the direction must be chosen
// before that re-render, so this watch runs on the default "pre" flush.
const sectionTransition = ref<"slide-next" | "slide-prev">("slide-next");
watch(currentPosition, (pos, previous) => {
  sectionTransition.value = pos >= previous ? "slide-next" : "slide-prev";
});

const isCurrentBookmarked = computed<boolean>(() =>
  currentSectionIndex.value === null
    ? false
    : isBookmarked(currentSectionIndex.value, currentParagraphIndex.value),
);

const bookTitle = ref("");

const returnLabel = computed<string>(() =>
  returnPoint.value === null ? "" : sectionLabel(manifest.value, returnPoint.value.sectionIndex),
);
const sectionProgress = computed<number>(() =>
  pageInfo.value.total > 0 ? (pageInfo.value.page + 1) / pageInfo.value.total : 0,
);
const totalWords = computed<number>(() =>
  manifest.value.reduce((sum, section) => sum + section.wordCount, 0),
);
// Word-weighted progress through the whole book: every word in sections already
// read, plus the within-section page fraction of the current one. Word counts (not
// section index) because section lengths vary wildly — a one-line title page next
// to a long chapter — so a section-index percent would lurch.
const bookProgress = computed<number>(() => {
  const total = totalWords.value;
  if (total === 0) {
    return 0;
  }
  let read = 0;
  for (let i = 0; i < currentPosition.value; i++) {
    read += manifest.value[i]?.wordCount ?? 0;
  }
  read += sectionProgress.value * (manifest.value[currentPosition.value]?.wordCount ?? 0);
  return Math.min(read / total, 1);
});
const bookPercentLabel = computed<string>(() => `${String(Math.round(bookProgress.value * 100))}%`);
// What the assistant treats as "read so far" in the current section: the end
// offset of the paragraph at the top of the page (the spoiler boundary).
const currentLocation = computed<number>(
  () =>
    currentSection.value?.paragraphs.find((p) => p.paragraphIndex === currentParagraphIndex.value)
      ?.locationEnd ?? 0,
);
const cachingLabel = computed<string>(() =>
  progress.value ? `${String(progress.value.cached)}/${String(progress.value.total)}` : "",
);
// While the book is still being processed, the AI tabs legitimately have no data
// yet — drives a "still processing" empty state instead of "nothing here".
const isMemoryProcessing = computed<boolean>(() => {
  const status = books.value.find((book) => book.bookId === bookId)?.processingStatus;
  return status !== undefined && status !== "Indexed" && status !== "Failed";
});
// The book's extraction version, threaded into the Memory caches so a reprocess
// (which bumps `updatedAt`) invalidates them. Null until the library list is known
// — then the caches simply don't gate on version.
const bookVersion = computed<string | null>(
  () => books.value.find((book) => book.bookId === bookId)?.updatedAt ?? null,
);

// start() restarts the countdown on each reveal; stop() cancels it when chrome is
// dismissed by hand.
const { start: scheduleHide, stop: cancelHide } = useTimeoutFn(
  () => {
    if (
      !settingsOpen.value &&
      !bookmarksOpen.value &&
      !contentsOpen.value &&
      !memoryPanelOpen.value
    ) {
      chromeVisible.value = false;
    }
  },
  CHROME_IDLE_MS,
  { immediate: false },
);

function revealChrome(): void {
  chromeVisible.value = true;
  scheduleHide();
}

function toggleChrome(): void {
  if (chromeVisible.value) {
    chromeVisible.value = false;
    cancelHide();
  } else {
    revealChrome();
  }
}

function openSettings(): void {
  settingsOpen.value = true;
  chromeVisible.value = true;
}

function openBookmarks(): void {
  bookmarksOpen.value = true;
  chromeVisible.value = true;
}

function openContents(): void {
  contentsOpen.value = true;
  chromeVisible.value = true;
}

// Jump to the start of a section chosen from the contents list.
async function jumpToSection(sectionIndex: number): Promise<void> {
  contentsOpen.value = false;
  await goTo(sectionIndex, 0);
}

// The toolbar entry opens the Memory panel to the tab the reader left it on
// (restored on book open). The selection popover's ask flow forces the Assistant
// tab below, since that's an explicit "ask" intent.
function openMemoryPanel(): void {
  memoryPanelOpen.value = true;
  chromeVisible.value = true;
}

// "Ask…" / "Tell me more" from the selection popover: pre-fill the assistant
// input, switch to the Assistant tab, and open the panel.
function onAskFromSelection(text: string): void {
  assistantDraft.value = text;
  selection.value = null;
  setMemoryTab("assistant");
  memoryPanelOpen.value = true;
  chromeVisible.value = true;
}

// Open the spoiler-safe entity popup at the current position (from a highlighted
// name tap, the Story Guide, or the selection popover's "Tell me more").
function onEntityTap(payload: { entityId: string; name: string }): void {
  const sectionIndex = currentSectionIndex.value;
  if (sectionIndex === null) {
    return;
  }
  void openEntitySummary(
    bookId,
    payload.entityId,
    payload.name,
    sectionIndex,
    currentLocation.value,
  );
}

// Same, but dismiss the selection popover the request came from.
function onOpenEntityFromSelection(payload: { entityId: string; name: string }): void {
  selection.value = null;
  onEntityTap(payload);
}

// Following a source or timeline event: remember the current spot (so we can offer
// a way back), close the panel, navigate to the passage, and flash it. Only the
// first jump in a run captures the origin, so hopping between targets still returns
// to where reading actually left off.
async function jumpToLocation(sectionIndex: number, locationStart: number): Promise<void> {
  const here = currentSectionIndex.value;
  if (here !== null && returnPoint.value === null) {
    returnPoint.value = { sectionIndex: here, paragraphIndex: currentParagraphIndex.value };
  }
  memoryPanelOpen.value = false;
  const paragraphIndex = await goToLocation(sectionIndex, locationStart);
  if (paragraphIndex !== null) {
    flashParagraph(paragraphIndex);
  }
}

function onJumpToSource(source: AssistantSource): void {
  void jumpToLocation(source.sectionIndex, source.locationStart);
}

function onJumpToTimeline(payload: { sectionIndex: number; locationStart: number }): void {
  void jumpToLocation(payload.sectionIndex, payload.locationStart);
}

// Return to the reading position captured before following a source, flash it to
// re-orient, and dismiss the affordance.
async function returnToReading(): Promise<void> {
  const point = returnPoint.value;
  if (point === null) {
    return;
  }
  await goTo(point.sectionIndex, point.paragraphIndex);
  flashParagraph(point.paragraphIndex);
  returnPoint.value = null;
}

// Dismiss the "Back to…" affordance without navigating (the X on the split button).
function dismissReturn(): void {
  returnPoint.value = null;
}

// Bookmark (or un-bookmark) the paragraph at the top of the current page, with a
// short text preview as its label.
async function toggleBookmark(): Promise<void> {
  const sectionIndex = currentSectionIndex.value;
  const paragraphIndex = currentParagraphIndex.value;
  const paragraph = currentSection.value?.paragraphs.find(
    (p) => p.paragraphIndex === paragraphIndex,
  );
  if (sectionIndex === null || !paragraph) {
    return;
  }
  const label = paragraph.text.slice(0, 80).trim();
  await toggle(
    bookId,
    { sectionIndex, paragraphIndex, locationStart: paragraph.locationStart },
    label.length > 0 ? label : null,
  );
}

async function jumpToBookmark(sectionIndex: number, paragraphIndex: number): Promise<void> {
  bookmarksOpen.value = false;
  await goTo(sectionIndex, paragraphIndex);
}

async function goNext(): Promise<void> {
  const advanced = await nextSection();
  // Recap fires only on a forward advance into a new section (not on resume,
  // backward paging, or jumps) — entering section N shows the through-(N-1) memory.
  if (advanced && currentSectionIndex.value !== null) {
    void enterSection(
      bookId,
      currentSectionIndex.value,
      bookVersion.value,
      !isMemoryProcessing.value,
    );
  }
}

async function goPrev(): Promise<void> {
  await prevSection();
}

function onPageChange(page: number, total: number): void {
  pageInfo.value = { page, total };
}

// Track the current paragraph and record the position (throttled in the
// composable). locationStart/End come from the paragraph's offsets.
function onParagraph(paragraphIndex: number): void {
  setParagraph(paragraphIndex);
  const sectionIndex = currentSectionIndex.value;
  const paragraph = currentSection.value?.paragraphs.find(
    (p) => p.paragraphIndex === paragraphIndex,
  );
  if (sectionIndex !== null && paragraph) {
    void record(bookId, {
      sectionIndex,
      paragraphIndex,
      pageIndex: pageInfo.value.page,
      locationStart: paragraph.locationStart,
      locationEnd: paragraph.locationEnd,
    });
  }
}

// The section manifest carries section titles, not the book's. The title comes
// from the library list, but resolving it must be best-effort: a deep-link or
// offline open may have no list yet, and that must never fail the reader (the
// book content is already served from Dexie).
async function resolveBookTitle(): Promise<string> {
  const cached = books.value.find((book) => book.bookId === bookId)?.title;
  if (cached !== undefined) {
    return cached;
  }
  try {
    await loadBooks();
  } catch {
    return "Reading";
  }
  return books.value.find((book) => book.bookId === bookId)?.title ?? "Reading";
}

async function load(): Promise<void> {
  phase.value = "loading";
  try {
    resetRecap();
    closeEntitySummary();
    resetAssistant();
    await loadSettings();
    // Refresh the library in the background so this book's extraction version
    // (`updatedAt`) is current — a reprocess while we were away then invalidates the
    // Memory caches. Best-effort: offline keeps the last-seen list.
    void loadBooks().catch(() => {});
    const position = await resume(bookId);
    // Open as soon as the manifest and the resume target section are available
    // (both cached-first), so a partially-cached book opens offline at a cached
    // section. Whole-book prefetch then continues in the background — a missing
    // section there must not block or fail the reader (the cache progress chip
    // surfaces that it isn't fully downloaded yet).
    await open(bookId, position ?? undefined);
    bookTitle.value = await resolveBookTitle();
    phase.value = "ready";
    void requestWakeLock("screen").catch(() => undefined);
    revealChrome();
    void ensureCached(bookId).catch(() => {});
    void loadBookmarks(bookId);
    void restoreMemoryTab(bookId);
    // After a >24h gap, recap the story up to where the reader is resuming.
    if (currentSectionIndex.value !== null) {
      void maybeShowPreviously(bookId, currentSectionIndex.value, currentLocation.value);
    }
  } catch {
    phase.value = "error";
  }
}

// Desktop shortcuts: `g m` opens the Memory panel; number keys switch tabs while
// it's open. Ignored while typing in an input (the question box, the Story Guide
// search) so they never hijack text entry. (Esc-to-close is owned by each panel.)
let awaitingPanelKey = false;
// The 700ms window in which a `g` arms the next `m`; stop() closes it early once
// the chord resolves.
const { start: armPanelChord, stop: disarmPanelChord } = useTimeoutFn(
  () => {
    awaitingPanelKey = false;
  },
  700,
  { immediate: false },
);

function onReaderKeydown(event: KeyboardEvent): void {
  const target = event.target;
  const typing =
    target instanceof HTMLElement &&
    (target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.isContentEditable);
  if (typing || event.metaKey || event.ctrlKey || event.altKey) {
    return;
  }
  if (awaitingPanelKey) {
    awaitingPanelKey = false;
    disarmPanelChord();
    if (event.key === "m") {
      event.preventDefault();
      openMemoryPanel();
      return;
    }
  }
  if (event.key === "g") {
    awaitingPanelKey = true;
    armPanelChord();
    return;
  }
  if (memoryPanelOpen.value && /^[1-9]$/.test(event.key)) {
    const index = Number(event.key);
    if (index <= memoryTabCount) {
      event.preventDefault();
      selectMemoryTab(index);
    }
  }
}

useEventListener(window, "keydown", onReaderKeydown);

onMounted(() => {
  void load();
});

onBeforeUnmount(() => {
  // Push the last position now rather than waiting out the throttle window.
  void flush();
  void releaseWakeLock();
});
</script>

<template>
  <main
    class="relative h-dvh w-screen overflow-hidden bg-[var(--sm-bg)] text-[var(--sm-fg)]"
    :style="cssVars"
  >
    <div
      v-if="phase === 'loading'"
      class="flex h-full flex-col items-center justify-center gap-3 text-[var(--sm-muted)]"
    >
      <p class="text-sm">Caching book… {{ cachingLabel }}</p>
    </div>

    <div
      v-else-if="phase === 'error'"
      class="flex h-full flex-col items-center justify-center gap-3 px-6 text-center"
    >
      <p>This book isn't ready to read yet.</p>
      <div class="flex items-center gap-4">
        <button type="button" class="text-sm text-[var(--sm-accent)] hover:underline" @click="load">
          Try again
        </button>
        <RouterLink to="/" class="text-sm text-[var(--sm-muted)] hover:underline">
          Back to library
        </RouterLink>
      </div>
    </div>

    <template v-else>
      <ReaderToolbar
        :title="bookTitle"
        :section-title="currentSectionTitle"
        :book-percent="bookPercentLabel"
        :visible="chromeVisible"
        :offline="!isOnline"
        @open-settings="openSettings"
        @open-bookmarks="openBookmarks"
        @open-memory="openMemoryPanel"
        @open-contents="openContents"
      />

      <!-- PaginatedSection is a fragment (scroll div + teleported handles), which
           <Transition> can't animate; the single-root wrapper gives it one element
           to slide. -->
      <Transition :name="sectionTransition">
        <div v-if="currentSection" :key="currentSectionIndex ?? -1" class="reader-section">
          <PaginatedSection
            :section="currentSection"
            :entry-paragraph="entryParagraph"
            :entry-page="entryPage"
            :highlight="highlight"
            :selection-active="selection !== null"
            @update:paragraph="onParagraph"
            @page-change="onPageChange"
            @next-section="goNext"
            @prev-section="goPrev"
            @toggle-chrome="toggleChrome"
            @select="(payload) => (selection = payload)"
            @dismiss-select="selection = null"
            @entity-tap="onEntityTap"
          />
        </div>
      </Transition>

      <Transition
        enter-active-class="transition duration-200 ease-out"
        enter-from-class="translate-y-2 opacity-0"
        leave-active-class="transition duration-150 ease-in"
        leave-to-class="translate-y-2 opacity-0"
      >
        <div
          v-if="returnPoint"
          class="absolute bottom-6 left-1/2 z-20 flex max-w-[80%] -translate-x-1/2 items-center rounded-full bg-[var(--sm-accent)] text-sm font-medium text-white shadow-lg"
        >
          <button
            type="button"
            class="flex min-w-0 items-center gap-2 rounded-l-full py-2 pr-3 pl-4 hover:opacity-90"
            @click="returnToReading"
          >
            <svg
              class="size-4 shrink-0"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M9 14 4 9l5-5" />
              <path d="M4 9h11a5 5 0 0 1 0 10h-1" />
            </svg>
            <span class="truncate">Back to {{ returnLabel }}</span>
          </button>
          <span class="h-5 w-px shrink-0 bg-white/25" aria-hidden="true"></span>
          <button
            type="button"
            class="flex shrink-0 items-center rounded-r-full py-2 pr-4 pl-2 hover:opacity-90"
            aria-label="Dismiss"
            @click="dismissReturn"
          >
            <svg
              class="size-4"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M18 6 6 18" />
              <path d="m6 6 12 12" />
            </svg>
          </button>
        </div>
      </Transition>

      <div class="absolute inset-x-0 bottom-0 z-10 h-0.5 bg-[var(--sm-muted)]/15">
        <div
          class="h-full bg-[var(--sm-accent)] transition-[width] duration-200"
          :style="{ width: `${String(sectionProgress * 100)}%` }"
        ></div>
      </div>

      <ReaderSettingsDrawer :open="settingsOpen" @close="settingsOpen = false" />

      <BookmarksPanel
        :open="bookmarksOpen"
        :bookmarks="bookmarks"
        :manifest="manifest"
        :current-bookmarked="isCurrentBookmarked"
        @close="bookmarksOpen = false"
        @jump="(bookmark) => jumpToBookmark(bookmark.sectionIndex, bookmark.paragraphIndex)"
        @remove="removeById"
        @toggle-bookmark="toggleBookmark"
      />

      <ContentsPanel
        :open="contentsOpen"
        :manifest="manifest"
        :current-section-index="currentSectionIndex"
        @close="contentsOpen = false"
        @jump="jumpToSection"
      />

      <MemoryPanel
        :open="memoryPanelOpen"
        :book-id="bookId"
        :current-section="currentSectionIndex"
        :current-location="currentLocation"
        :cache-version="bookVersion"
        :manifest="manifest"
        :is-processing="isMemoryProcessing"
        @close="memoryPanelOpen = false"
        @jump="onJumpToSource"
        @jump-location="onJumpToTimeline"
      />

      <SelectionPopover
        v-if="selection"
        :selection="selection"
        :book-id="bookId"
        :current-section="currentSectionIndex"
        :current-location="currentLocation"
        @ask="onAskFromSelection"
        @open-entity="onOpenEntityFromSelection"
        @close="selection = null"
      />

      <EntityPopup />

      <Transition
        enter-active-class="transition duration-200 ease-out"
        enter-from-class="-translate-y-3 opacity-0"
        leave-active-class="transition duration-150 ease-in"
        leave-to-class="-translate-y-3 opacity-0"
      >
        <SectionRecapCard
          v-if="recapVisible && recapSnapshot"
          :snapshot="recapSnapshot"
          :manifest="manifest"
          @dismiss="dismissRecap"
          @dont-show-again="recapDontShowAgain"
        />
      </Transition>

      <Transition
        enter-active-class="transition duration-200 ease-out"
        enter-from-class="-translate-y-3 opacity-0"
        leave-active-class="transition duration-150 ease-in"
        leave-to-class="-translate-y-3 opacity-0"
      >
        <SectionRecapCard
          v-if="previouslyVisible && previouslySnapshot"
          heading="Previously…"
          :snapshot="previouslySnapshot"
          :manifest="manifest"
          @dismiss="dismissPreviously"
          @dont-show-again="previouslyDontShowAgain"
        />
      </Transition>
    </template>
  </main>
</template>

<style scoped>
.reader-section {
  height: 100%;
}

/* Chapter-change slide. During the turn both sections are absolutely stacked so
   the old one slides out as the new one slides in; the duration/easing mirror the
   within-chapter page-flip (SWIPE_ANIM_MS / easeOutCubic in PaginatedSection) so a
   chapter boundary feels continuous with paging inside one. */
.slide-next-enter-active,
.slide-next-leave-active,
.slide-prev-enter-active,
.slide-prev-leave-active {
  position: absolute;
  inset: 0;
  transition: transform 280ms cubic-bezier(0.33, 1, 0.68, 1);
}

.slide-next-enter-from {
  transform: translateX(100%);
}

.slide-next-leave-to {
  transform: translateX(-100%);
}

.slide-prev-enter-from {
  transform: translateX(-100%);
}

.slide-prev-leave-to {
  transform: translateX(100%);
}

@media (prefers-reduced-motion: reduce) {
  .slide-next-enter-active,
  .slide-next-leave-active,
  .slide-prev-enter-active,
  .slide-prev-leave-active {
    transition-duration: 0ms;
  }

  .slide-next-enter-from,
  .slide-next-leave-to,
  .slide-prev-enter-from,
  .slide-prev-leave-to {
    transform: none;
  }
}
</style>

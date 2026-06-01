<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import type { AssistantSource } from "@storymark/shared-types";
import BookmarksPanel from "@/components/reader/BookmarksPanel.vue";
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
  sectionCount,
  currentSectionTitle,
  nextSection,
  prevSection,
  setParagraph,
  open,
  goTo,
  goToLocation,
} = useReader();
const { record, resume, flush } = useReadingProgress();
const { bookmarks, load: loadBookmarks, toggle, removeById, isBookmarked } = useBookmarks();
const { draft: assistantDraft } = useAssistant();
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

const phase = ref<"loading" | "ready" | "error">("loading");
const chromeVisible = ref(true);
const settingsOpen = ref(false);
const bookmarksOpen = ref(false);
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
// Captured on the first source jump; cleared on return or on reading onward.
const returnPoint = ref<{ sectionIndex: number; paragraphIndex: number } | null>(null);
const pageInfo = ref<{ page: number; total: number }>({ page: 0, total: 1 });

const isCurrentBookmarked = computed<boolean>(() =>
  currentSectionIndex.value === null
    ? false
    : isBookmarked(currentSectionIndex.value, currentParagraphIndex.value),
);

const bookTitle = ref("");

const positionLabel = computed<string>(
  () => `${String(currentPosition.value + 1)} / ${String(sectionCount.value)}`,
);
const returnLabel = computed<string>(() =>
  returnPoint.value === null ? "" : sectionLabel(manifest.value, returnPoint.value.sectionIndex),
);
const sectionProgress = computed<number>(() =>
  pageInfo.value.total > 0 ? (pageInfo.value.page + 1) / pageInfo.value.total : 0,
);
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

let idleTimer: number | undefined;

function scheduleHide(): void {
  if (idleTimer !== undefined) {
    window.clearTimeout(idleTimer);
  }
  idleTimer = window.setTimeout(() => {
    if (!settingsOpen.value && !bookmarksOpen.value && !memoryPanelOpen.value) {
      chromeVisible.value = false;
    }
  }, CHROME_IDLE_MS);
}

function revealChrome(): void {
  chromeVisible.value = true;
  scheduleHide();
}

function toggleChrome(): void {
  if (chromeVisible.value) {
    chromeVisible.value = false;
    if (idleTimer !== undefined) {
      window.clearTimeout(idleTimer);
    }
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
  returnPoint.value = null;
  const advanced = await nextSection();
  // Recap fires only on a forward advance into a new section (not on resume,
  // backward paging, or jumps) — entering section N shows the through-(N-1) memory.
  if (advanced && currentSectionIndex.value !== null) {
    void enterSection(bookId, currentSectionIndex.value);
  }
}

async function goPrev(): Promise<void> {
  returnPoint.value = null;
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
    await loadSettings();
    await ensureCached(bookId);
    const position = await resume(bookId);
    await open(bookId, position ?? undefined);
    bookTitle.value = await resolveBookTitle();
    phase.value = "ready";
    revealChrome();
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
// it's open; Esc closes it. Ignored while typing in an input (the question box,
// the Story Guide search) so they never hijack text entry.
let awaitingPanelKey = false;
let panelKeyTimer: number | undefined;

function onReaderKeydown(event: KeyboardEvent): void {
  if (event.key === "Escape" && memoryPanelOpen.value) {
    memoryPanelOpen.value = false;
    return;
  }
  const target = event.target;
  const typing =
    target instanceof HTMLElement &&
    (target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.isContentEditable);
  if (typing || event.metaKey || event.ctrlKey || event.altKey) {
    return;
  }
  if (awaitingPanelKey) {
    awaitingPanelKey = false;
    window.clearTimeout(panelKeyTimer);
    if (event.key === "m") {
      event.preventDefault();
      openMemoryPanel();
      return;
    }
  }
  if (event.key === "g") {
    awaitingPanelKey = true;
    panelKeyTimer = window.setTimeout(() => {
      awaitingPanelKey = false;
    }, 700);
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

onMounted(() => {
  window.addEventListener("keydown", onReaderKeydown);
  void load();
});

onBeforeUnmount(() => {
  window.removeEventListener("keydown", onReaderKeydown);
  window.clearTimeout(panelKeyTimer);
  if (idleTimer !== undefined) {
    window.clearTimeout(idleTimer);
  }
  // Push the last position now rather than waiting out the throttle window.
  void flush();
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
        :position="positionLabel"
        :visible="chromeVisible"
        :offline="!isOnline"
        :bookmarked="isCurrentBookmarked"
        @open-settings="openSettings"
        @open-bookmarks="openBookmarks"
        @open-memory="openMemoryPanel"
        @toggle-bookmark="toggleBookmark"
      />

      <PaginatedSection
        v-if="currentSection"
        :section="currentSection"
        :entry-paragraph="entryParagraph"
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

      <Transition
        enter-active-class="transition duration-200 ease-out"
        enter-from-class="translate-y-2 opacity-0"
        leave-active-class="transition duration-150 ease-in"
        leave-to-class="translate-y-2 opacity-0"
      >
        <button
          v-if="returnPoint"
          type="button"
          class="absolute bottom-6 left-1/2 z-20 flex max-w-[80%] -translate-x-1/2 items-center gap-2 rounded-full bg-[var(--sm-accent)] px-4 py-2 text-sm font-medium text-white shadow-lg hover:opacity-90"
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
        @close="bookmarksOpen = false"
        @jump="(bookmark) => jumpToBookmark(bookmark.sectionIndex, bookmark.paragraphIndex)"
        @remove="removeById"
      />

      <MemoryPanel
        :open="memoryPanelOpen"
        :book-id="bookId"
        :current-section="currentSectionIndex"
        :current-location="currentLocation"
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

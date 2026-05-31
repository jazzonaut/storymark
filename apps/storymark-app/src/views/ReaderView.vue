<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import PaginatedSection from "@/components/reader/PaginatedSection.vue";
import ReaderSettingsDrawer from "@/components/reader/ReaderSettingsDrawer.vue";
import ReaderToolbar from "@/components/reader/ReaderToolbar.vue";
import { useBookCache } from "@/composables/use-book-cache";
import { useBooks } from "@/composables/use-books";
import { useOfflineSync } from "@/composables/use-offline-sync";
import { useReader } from "@/composables/use-reader";
import { useReaderSettings } from "@/composables/use-reader-settings";
import { useReadingProgress } from "@/composables/use-reading-progress";

const CHROME_IDLE_MS = 3500;

const route = useRoute();
const bookId = String(route.params.id);

const { books, loadBooks } = useBooks();
const { isOnline } = useOfflineSync();
const { cssVars, load: loadSettings } = useReaderSettings();
const { progress, ensureCached } = useBookCache();
const {
  currentSection,
  currentPosition,
  currentSectionIndex,
  entryParagraph,
  sectionCount,
  currentSectionTitle,
  nextSection,
  prevSection,
  setParagraph,
  open,
} = useReader();
const { record, resume, flush } = useReadingProgress();

const phase = ref<"loading" | "ready" | "error">("loading");
const chromeVisible = ref(true);
const settingsOpen = ref(false);
const pageInfo = ref<{ page: number; total: number }>({ page: 0, total: 1 });

const bookTitle = ref("");

const positionLabel = computed<string>(
  () => `${String(currentPosition.value + 1)} / ${String(sectionCount.value)}`,
);
const sectionProgress = computed<number>(() =>
  pageInfo.value.total > 0 ? (pageInfo.value.page + 1) / pageInfo.value.total : 0,
);
const cachingLabel = computed<string>(() =>
  progress.value ? `${String(progress.value.cached)}/${String(progress.value.total)}` : "",
);

let idleTimer: number | undefined;

function scheduleHide(): void {
  if (idleTimer !== undefined) {
    window.clearTimeout(idleTimer);
  }
  idleTimer = window.setTimeout(() => {
    if (!settingsOpen.value) {
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

async function goNext(): Promise<void> {
  await nextSection();
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

onMounted(async () => {
  try {
    await loadSettings();
    await ensureCached(bookId);
    const position = await resume(bookId);
    await open(bookId, position ?? undefined);
    bookTitle.value = await resolveBookTitle();
    phase.value = "ready";
    revealChrome();
  } catch {
    phase.value = "error";
  }
});

onBeforeUnmount(() => {
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
      <RouterLink to="/" class="text-sm text-[var(--sm-accent)] hover:underline">
        Back to library
      </RouterLink>
    </div>

    <template v-else>
      <ReaderToolbar
        :title="bookTitle"
        :section-title="currentSectionTitle"
        :position="positionLabel"
        :visible="chromeVisible"
        :offline="!isOnline"
        @open-settings="openSettings"
      />

      <PaginatedSection
        v-if="currentSection"
        :section="currentSection"
        :entry-paragraph="entryParagraph"
        @update:paragraph="onParagraph"
        @page-change="onPageChange"
        @next-section="goNext"
        @prev-section="goPrev"
        @toggle-chrome="toggleChrome"
      />

      <div class="absolute inset-x-0 bottom-0 z-10 h-0.5 bg-[var(--sm-muted)]/15">
        <div
          class="h-full bg-[var(--sm-accent)] transition-[width] duration-200"
          :style="{ width: `${String(sectionProgress * 100)}%` }"
        ></div>
      </div>

      <ReaderSettingsDrawer :open="settingsOpen" @close="settingsOpen = false" />
    </template>
  </main>
</template>

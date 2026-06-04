<script setup lang="ts">
import { computed, ref } from "vue";
import { RouterLink } from "vue-router";
import type { BookSummary, ProcessingStatus } from "@storymark/shared-types";

const props = defineProps<{ book: BookSummary }>();

const emit = defineEmits<{
  (e: "request-delete", book: BookSummary): void;
  (e: "toggle-visibility", book: BookSummary): void;
}>();

const menuOpen = ref(false);

function requestDelete(): void {
  menuOpen.value = false;
  emit("request-delete", props.book);
}

function toggleVisibility(): void {
  menuOpen.value = false;
  emit("toggle-visibility", props.book);
}

// Sections exist from Parsed onward (S2.1); only then can the reader open.
const READABLE: ReadonlySet<ProcessingStatus> = new Set<ProcessingStatus>([
  "Parsed",
  "Chunking",
  "DetectingEntities",
  "ExtractingPerSection",
  "Embedding",
  "BuildingMemorySnapshots",
  "Indexed",
]);

const isReadable = computed<boolean>(() => READABLE.has(props.book.processingStatus));

const STATUS_STYLES: Record<ProcessingStatus, string> = {
  Uploaded: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200",
  Parsing: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200",
  Parsed: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200",
  Chunking: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200",
  DetectingEntities: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200",
  ExtractingPerSection: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200",
  Embedding: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200",
  BuildingMemorySnapshots: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200",
  Indexed: "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200",
  Failed: "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200",
};

const statusClass = computed<string>(() => STATUS_STYLES[props.book.processingStatus]);

// Extraction and embedding are the long phases with a real denominator (sections,
// then chunks); show each one's percentage on the badge. Remaining phases have no
// honest fraction, so they show the bare status.
const statusLabel = computed<string>(() => {
  const book = props.book;
  if (book.processingStatus === "ExtractingPerSection" && book.extractionProgress) {
    const { extracted, total } = book.extractionProgress;
    if (total > 0) {
      const pct = Math.min(100, Math.round((extracted / total) * 100));
      return `Extracting ${pct}%`;
    }
  }
  if (book.processingStatus === "Embedding" && book.embeddingProgress) {
    const { embedded, total } = book.embeddingProgress;
    if (total > 0) {
      const pct = Math.min(100, Math.round((embedded / total) * 100));
      return `Embedding ${pct}%`;
    }
  }
  return book.processingStatus;
});

const coverFailed = ref(false);
</script>

<template>
  <div class="relative flex flex-col">
    <component
      :is="isReadable ? RouterLink : 'article'"
      v-bind="isReadable ? { to: { name: 'reader', params: { id: book.bookId } } } : {}"
      class="flex flex-col"
      :class="isReadable ? 'group cursor-pointer' : ''"
    >
      <div
        class="relative flex aspect-[2/3] items-center justify-center overflow-hidden rounded-lg bg-zinc-100 transition group-hover:opacity-90 dark:bg-zinc-800"
      >
        <img
          v-if="!coverFailed"
          :src="book.coverUrl"
          :alt="`Cover of ${book.title}`"
          class="h-full w-full object-cover"
          @error="coverFailed = true"
        />
        <span v-else class="px-2 text-center text-xs text-[var(--sm-muted)]">{{ book.title }}</span>
        <span
          class="absolute bottom-1 left-1 inline-block w-fit rounded-sm px-1.5 py-px text-[10px] font-medium opacity-90"
          :class="statusClass"
        >
          {{ statusLabel }}
        </span>
        <span
          v-if="book.isPublic"
          class="absolute bottom-1 right-1 inline-block w-fit rounded-sm bg-sky-100 px-1.5 py-px text-[10px] font-medium text-sky-800 opacity-90 dark:bg-sky-900 dark:text-sky-200"
          :title="
            book.isOwned ? 'Visible to everyone in the household' : 'Shared by another reader'
          "
        >
          Public
        </span>
      </div>
      <h2 class="mt-2 line-clamp-2 text-sm font-medium" :title="book.title">{{ book.title }}</h2>
      <p class="text-xs text-[var(--sm-muted)]">{{ book.author ?? "Unknown author" }}</p>
      <p
        v-if="book.processingError"
        class="mt-1 text-xs text-red-700 dark:text-red-300"
        :title="book.processingError"
      >
        {{ book.processingError }}
      </p>
    </component>

    <!-- Owner-only: a public book from another reader has no actions (delete and
         visibility are owner-gated server-side, ADR-0023). -->
    <button
      v-if="book.isOwned"
      type="button"
      aria-label="Book actions"
      class="absolute right-1.5 top-1.5 z-10 flex size-7 items-center justify-center rounded-full bg-black/45 text-white backdrop-blur-sm transition hover:bg-black/65"
      @click="menuOpen = !menuOpen"
    >
      ⋯
    </button>

    <template v-if="menuOpen">
      <div class="fixed inset-0 z-10" @click="menuOpen = false"></div>
      <div
        class="absolute right-1.5 top-9 z-20 w-32 overflow-hidden rounded-md border border-[var(--sm-muted)]/20 bg-[var(--sm-bg)] py-1 shadow-lg"
      >
        <button
          type="button"
          class="block w-full px-3 py-1.5 text-left text-sm hover:bg-[var(--sm-surface)]"
          @click="toggleVisibility"
        >
          {{ book.isPublic ? "Make private" : "Make public" }}
        </button>
        <button
          type="button"
          class="block w-full px-3 py-1.5 text-left text-sm text-red-700 hover:bg-[var(--sm-surface)] dark:text-red-300"
          @click="requestDelete"
        >
          Delete
        </button>
      </div>
    </template>
  </div>
</template>

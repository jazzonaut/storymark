<script setup lang="ts">
import { computed, ref } from "vue";
import { RouterLink } from "vue-router";
import type { BookSummary, ProcessingStatus } from "@storymark/shared-types";

const props = defineProps<{ book: BookSummary }>();

const emit = defineEmits<{ (e: "request-delete", book: BookSummary): void }>();

const menuOpen = ref(false);

function requestDelete(): void {
  menuOpen.value = false;
  emit("request-delete", props.book);
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

// Embedding is the long phase with a real denominator (chunk count); show its
// percentage on the badge. Other phases have no honest fraction, so they don't.
const statusLabel = computed<string>(() => {
  const progress = props.book.embeddingProgress;
  if (props.book.processingStatus === "Embedding" && progress && progress.total > 0) {
    const pct = Math.min(100, Math.round((progress.embedded / progress.total) * 100));
    return `Embedding ${pct}%`;
  }
  return props.book.processingStatus;
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
        class="flex aspect-[2/3] items-center justify-center overflow-hidden rounded-lg bg-zinc-100 transition group-hover:opacity-90 dark:bg-zinc-800"
      >
        <img
          v-if="!coverFailed"
          :src="book.coverUrl"
          :alt="`Cover of ${book.title}`"
          class="h-full w-full object-cover"
          @error="coverFailed = true"
        />
        <span v-else class="px-2 text-center text-xs text-[var(--sm-muted)]">{{ book.title }}</span>
      </div>
      <h2 class="mt-2 line-clamp-2 text-sm font-medium" :title="book.title">{{ book.title }}</h2>
      <p class="text-xs text-[var(--sm-muted)]">{{ book.author ?? "Unknown author" }}</p>
      <span
        class="mt-1 inline-block w-fit rounded-full px-2 py-0.5 text-xs font-medium"
        :class="statusClass"
      >
        {{ statusLabel }}
      </span>
      <p
        v-if="book.processingError"
        class="mt-1 text-xs text-red-700 dark:text-red-300"
        :title="book.processingError"
      >
        {{ book.processingError }}
      </p>
    </component>

    <button
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
          class="block w-full px-3 py-1.5 text-left text-sm text-red-700 hover:bg-[var(--sm-surface)] dark:text-red-300"
          @click="requestDelete"
        >
          Delete
        </button>
      </div>
    </template>
  </div>
</template>

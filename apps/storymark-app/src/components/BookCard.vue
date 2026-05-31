<script setup lang="ts">
import { computed, ref } from "vue";
import { RouterLink } from "vue-router";
import type { BookSummary, ProcessingStatus } from "@storymark/shared-types";

const props = defineProps<{ book: BookSummary }>();

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

const coverFailed = ref(false);
</script>

<template>
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
      {{ book.processingStatus }}
    </span>
  </component>
</template>

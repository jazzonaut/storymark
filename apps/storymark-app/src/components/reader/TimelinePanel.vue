<script setup lang="ts">
import { watch } from "vue";
import type { SectionSummary } from "@storymark/shared-types";
import MemoryEmptyState from "@/components/reader/MemoryEmptyState.vue";
import MemoryStaleBadge from "@/components/reader/MemoryStaleBadge.vue";
import TimelineList from "@/components/reader/TimelineList.vue";
import { useTimeline } from "@/composables/use-timeline";
import { useEntityMerge } from "@/composables/use-entity-merge";

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  cacheVersion: string | null;
  manifest: readonly SectionSummary[];
  isProcessing: boolean;
}>();

const emit = defineEmits<{
  (e: "jump", payload: { sectionIndex: number; locationStart: number }): void;
}>();

const { events, entityNames, isLoading, error, isStale, load } = useTimeline();
const { mergeRevision } = useEntityMerge();

// The reader's spoiler boundary is (section, location) and a reprocess changes
// cacheVersion, so all three must drive the reload — watching section alone would
// leave the panel showing data for the wrong boundary after an in-section move. A merge
// (mergeRevision) reloads too, since it rewrites the events' involved entities.
watch(
  () =>
    [
      props.bookId,
      props.currentSection,
      props.currentLocation,
      props.cacheVersion,
      mergeRevision.value,
    ] as const,
  () => {
    if (props.currentSection !== null) {
      void load(
        props.bookId,
        props.currentSection,
        props.currentLocation,
        props.cacheVersion,
        !props.isProcessing,
      );
    }
  },
  { immediate: true },
);
</script>

<template>
  <div class="flex h-full flex-col gap-3">
    <div v-if="isLoading" class="h-56 w-full animate-pulse rounded-lg bg-[var(--sm-surface)]"></div>

    <p v-else-if="error" class="text-sm text-[var(--sm-muted)]">{{ error }}</p>

    <MemoryEmptyState
      v-else-if="events.length === 0"
      :processing="isProcessing"
      idle="No events yet — they appear here as you read through the story."
    />

    <template v-else>
      <MemoryStaleBadge v-if="isStale" />
      <TimelineList
        :events="events"
        :entity-names="entityNames"
        :manifest="manifest"
        @jump="(payload) => emit('jump', payload)"
      />
    </template>
  </div>
</template>

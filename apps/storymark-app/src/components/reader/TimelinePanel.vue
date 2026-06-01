<script setup lang="ts">
import { watch } from "vue";
import type { SectionSummary } from "@storymark/shared-types";
import MemoryEmptyState from "@/components/reader/MemoryEmptyState.vue";
import TimelineList from "@/components/reader/TimelineList.vue";
import { useTimeline } from "@/composables/use-timeline";

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  manifest: readonly SectionSummary[];
  isProcessing: boolean;
}>();

const emit = defineEmits<{
  (e: "jump", payload: { sectionIndex: number; locationStart: number }): void;
}>();

const { events, entityNames, isLoading, error, load } = useTimeline();

watch(
  () => [props.bookId, props.currentSection] as const,
  () => {
    if (props.currentSection !== null) {
      void load(props.bookId, props.currentSection, props.currentLocation);
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

    <TimelineList
      v-else
      :events="events"
      :entity-names="entityNames"
      :manifest="manifest"
      @jump="(payload) => emit('jump', payload)"
    />
  </div>
</template>

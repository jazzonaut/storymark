<script setup lang="ts">
import { defineAsyncComponent, ref, shallowRef, watch } from "vue";
import type { BookEvent, SectionSummary } from "@storymark/shared-types";
import EventDetailCard from "@/components/reader/EventDetailCard.vue";
import { useTimeline } from "@/composables/use-timeline";
import { sectionLabel } from "@/utils/section-label";

// ECharts is heavy; load it only when the timeline tab is actually opened.
const TimelineChart = defineAsyncComponent(() => import("@/components/reader/TimelineChart.vue"));

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  manifest: readonly SectionSummary[];
}>();

const emit = defineEmits<{
  (e: "jump", payload: { sectionIndex: number; locationStart: number }): void;
}>();

const { events, entityNames, isLoading, error, load } = useTimeline();
const selected = shallowRef<BookEvent | null>(null);

function onSelect(event: BookEvent): void {
  selected.value = event;
}

watch(
  () => [props.bookId, props.currentSection] as const,
  () => {
    selected.value = null;
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

    <p v-else-if="events.length === 0" class="text-sm text-[var(--sm-muted)]">
      No events yet — they appear here as you read through the story.
    </p>

    <template v-else>
      <TimelineChart :events="events" :entity-names="entityNames" @select="onSelect" />
      <p v-if="selected === null" class="text-center text-xs text-[var(--sm-muted)]">
        Tap a point to see the event.
      </p>
      <EventDetailCard
        v-else
        :event="selected"
        :entity-names="entityNames"
        :section-label="sectionLabel(manifest, selected.sectionIndex)"
        @jump="(payload) => emit('jump', payload)"
      />
    </template>
  </div>
</template>

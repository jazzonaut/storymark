<script setup lang="ts">
import { computed } from "vue";
import type { BookEvent, SectionSummary } from "@storymark/shared-types";
import { eventTypeColor, eventTypeLabel } from "@/utils/event-type";
import { sectionLabel } from "@/utils/section-label";

const props = defineProps<{
  events: readonly BookEvent[];
  entityNames: ReadonlyMap<string, string>;
  manifest: readonly SectionSummary[];
}>();

const emit = defineEmits<{
  (e: "jump", payload: { sectionIndex: number; locationStart: number }): void;
}>();

interface TimelineRow {
  readonly event: BookEvent;
  readonly section: string;
  readonly involved: string;
}

// Chronological order = top-to-bottom down the timeline spine.
const rows = computed<readonly TimelineRow[]>(() =>
  props.events
    .toSorted((a, b) =>
      a.sectionIndex === b.sectionIndex
        ? a.locationStart - b.locationStart
        : a.sectionIndex - b.sectionIndex,
    )
    .map((event) => ({
      event,
      section: sectionLabel(props.manifest, event.sectionIndex),
      involved: event.involvedEntityIds
        .map((id) => props.entityNames.get(id))
        .filter((name): name is string => name !== undefined)
        .join(", "),
    })),
);
</script>

<template>
  <ol class="flex flex-col">
    <li
      v-for="(row, index) in rows"
      :key="`${String(row.event.sectionIndex)}-${String(row.event.locationStart)}-${String(index)}`"
      class="relative pb-5 pl-6 last:pb-0"
    >
      <span
        v-if="index < rows.length - 1"
        class="absolute top-2 -bottom-1 left-[5px] w-0.5 bg-[var(--sm-muted)]/20"
        aria-hidden="true"
      ></span>
      <span
        class="absolute left-0 top-1 size-3 rounded-full ring-2 ring-[var(--sm-bg)]"
        :style="{ backgroundColor: eventTypeColor(row.event.eventType) }"
        aria-hidden="true"
      ></span>
      <div class="flex flex-wrap items-baseline gap-x-2">
        <span class="text-sm font-semibold text-[var(--sm-fg)]">
          {{ eventTypeLabel(row.event.eventType) }}
        </span>
        <span class="text-xs text-[var(--sm-muted)]">· {{ row.section }}</span>
      </div>
      <p class="mt-1 text-sm leading-relaxed text-[var(--sm-fg)]">{{ row.event.description }}</p>
      <p v-if="row.involved.length > 0" class="mt-0.5 text-xs text-[var(--sm-muted)]">
        {{ row.involved }}
      </p>
      <button
        type="button"
        class="mt-1.5 text-sm font-medium text-[var(--sm-accent)] hover:underline"
        @click="
          emit('jump', {
            sectionIndex: row.event.sectionIndex,
            locationStart: row.event.locationStart,
          })
        "
      >
        Go to passage →
      </button>
    </li>
  </ol>
</template>

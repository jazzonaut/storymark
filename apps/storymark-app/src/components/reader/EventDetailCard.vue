<script setup lang="ts">
import { computed } from "vue";
import type { BookEvent } from "@storymark/shared-types";
import { eventTypeColor, eventTypeLabel } from "@/utils/event-type";

const props = defineProps<{
  event: BookEvent;
  entityNames: ReadonlyMap<string, string>;
  sectionLabel: string;
}>();

const emit = defineEmits<{
  (e: "jump", payload: { sectionIndex: number; locationStart: number }): void;
}>();

const involved = computed<readonly string[]>(() =>
  props.event.involvedEntityIds
    .map((id) => props.entityNames.get(id))
    .filter((name): name is string => name !== undefined),
);
</script>

<template>
  <div class="rounded-lg border border-[var(--sm-muted)]/15 bg-[var(--sm-surface)]/40 p-3">
    <div class="flex items-center gap-2">
      <span
        class="size-2.5 shrink-0 rounded-full"
        :style="{ backgroundColor: eventTypeColor(event.eventType) }"
      ></span>
      <span class="text-sm font-semibold text-[var(--sm-fg)]">
        {{ eventTypeLabel(event.eventType) }}
      </span>
      <span class="text-xs text-[var(--sm-muted)]">· {{ sectionLabel }}</span>
    </div>
    <p class="mt-2 text-sm leading-relaxed text-[var(--sm-fg)]">{{ event.description }}</p>
    <p v-if="involved.length > 0" class="mt-1 text-xs text-[var(--sm-muted)]">
      {{ involved.join(", ") }}
    </p>
    <button
      type="button"
      class="mt-2 text-sm font-medium text-[var(--sm-accent)] hover:underline"
      @click="
        emit('jump', { sectionIndex: event.sectionIndex, locationStart: event.locationStart })
      "
    >
      Go to passage →
    </button>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import type { BookEvent, SectionSummary, StoryGuideEntity } from "@storymark/shared-types";
import { sectionLabel } from "@/utils/section-label";
import { characterColor } from "@/utils/character-color";
import { useCharacterPalette } from "@/composables/use-character-palette";

const props = defineProps<{
  events: readonly BookEvent[];
  characters: readonly StoryGuideEntity[];
  manifest: readonly SectionSummary[];
  section: number;
  location: number;
}>();
const emit = defineEmits<{
  (e: "jump", payload: { sectionIndex: number; locationStart: number }): void;
}>();

const selected = ref("");
const recent = computed(() =>
  props.events
    .filter(
      (event) =>
        event.sectionIndex >= props.section - 2 &&
        (event.sectionIndex < props.section ||
          (event.sectionIndex === props.section && event.locationEnd <= props.location)),
    )
    .toSorted((a, b) => a.sectionIndex - b.sectionIndex || a.locationStart - b.locationStart),
);

function appearances(events: readonly BookEvent[]): Map<string, number> {
  const counts = new Map<string, number>();
  for (const event of events) {
    for (const id of new Set(event.involvedEntityIds)) {
      counts.set(id, (counts.get(id) ?? 0) + 1);
    }
  }
  return counts;
}

const cast = computed(() => {
  const counts = appearances(recent.value);
  return props.characters
    .filter((person) => counts.has(person.entityId))
    .toSorted(
      (a, b) =>
        (counts.get(b.entityId) ?? 0) - (counts.get(a.entityId) ?? 0) ||
        a.canonicalName.localeCompare(b.canonicalName),
    );
});
const palette = useCharacterPalette();
watch(
  () => {
    const visible = new Set(cast.value.map((person) => person.entityId));
    return recent.value.map((event) => event.involvedEntityIds.filter((id) => visible.has(id)));
  },
  (groups) => {
    void palette.register("braid", groups);
  },
  { immediate: true },
);
const focus = computed(
  () => cast.value.find((person) => person.entityId === selected.value) ?? cast.value[0],
);
const strands = computed(() => {
  const lead = focus.value;
  if (!lead) return [];
  const together = appearances(
    recent.value.filter((event) => event.involvedEntityIds.includes(lead.entityId)),
  );
  const companions = cast.value
    .filter((person) => person.entityId !== lead.entityId && together.has(person.entityId))
    .toSorted((a, b) => (together.get(b.entityId) ?? 0) - (together.get(a.entityId) ?? 0))
    .slice(0, 2);
  return [lead, ...companions].map((person, i) => ({
    id: person.entityId,
    name: person.canonicalName,
    color: characterColor(person.entityId),
    x: 8 + i * 16,
  }));
});

const rows = computed(() => {
  const events = recent.value.filter((event) =>
    strands.value.some((s) => event.involvedEntityIds.includes(s.id)),
  );
  const lanes = strands.value.map((s) => ({
    ...s,
    first: events.findIndex((event) => event.involvedEntityIds.includes(s.id)),
    last: events.findLastIndex((event) => event.involvedEntityIds.includes(s.id)),
  }));
  return events.map((event, i) => {
    const present = lanes.filter((s) => event.involvedEntityIds.includes(s.id));
    const junction = present[0]?.x ?? 8;
    const paths = lanes
      .filter((s) => i >= s.first && i <= s.last)
      .map((s) => {
        const mid = event.involvedEntityIds.includes(s.id) ? junction : s.x;
        const start = i === s.first ? 25 : 0;
        const upper = `M ${s.x} ${start} C ${s.x} 32 ${mid} 32 ${mid} 50`;
        const lower = i < s.last ? `M ${mid} 50 C ${mid} 72 ${s.x} 72 ${s.x} 100` : "";
        return { ...s, path: upper + lower };
      });
    return {
      event,
      paths,
      junction,
      names: present.map((s) => s.name).join(" · "),
      section: sectionLabel(props.manifest, event.sectionIndex),
    };
  });
});
</script>

<template>
  <p v-if="!focus" class="text-sm text-[var(--sm-muted)]">
    No named characters are linked to events in the last three sections at this position.
  </p>
  <div v-else class="space-y-3">
    <label class="flex items-center gap-2 text-sm font-semibold">
      Follow
      <select
        class="min-w-0 flex-1 rounded-md border border-[var(--sm-muted)]/30 bg-[var(--sm-bg)] px-2 py-2"
        :value="focus.entityId"
        @change="selected = ($event.target as HTMLSelectElement).value"
      >
        <option v-for="person in cast" :key="person.entityId" :value="person.entityId">
          {{ person.canonicalName }}
        </option>
      </select>
    </label>
    <p class="text-xs text-[var(--sm-muted)]">
      Up to three sections ending at your page. Threads meet at shared events.
    </p>
    <ul aria-label="Character threads" class="flex flex-wrap gap-x-4 gap-y-1 text-xs">
      <li v-for="s in strands" :key="s.id" class="flex items-center gap-1.5">
        <span aria-hidden="true" class="h-0.5 w-4" :style="{ backgroundColor: s.color }"></span
        >{{ s.name }}
      </li>
    </ul>
    <ol>
      <li v-for="(row, index) in rows" :key="index" class="flex">
        <div aria-hidden="true" class="relative w-12 shrink-0">
          <svg
            viewBox="0 0 48 100"
            preserveAspectRatio="none"
            class="absolute inset-0 h-full w-full"
            fill="none"
          >
            <path
              v-for="s in row.paths"
              :key="s.id"
              :d="s.path"
              :stroke="s.color"
              stroke-width="2.5"
              vector-effect="non-scaling-stroke"
            />
          </svg>
          <span
            class="absolute top-1/2 size-2.5 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-[var(--sm-fg)] bg-[var(--sm-bg)]"
            :style="{ left: row.junction + 'px' }"
          ></span>
        </div>
        <button
          type="button"
          class="min-w-0 flex-1 rounded-md px-2 py-3 text-left hover:bg-[var(--sm-surface)]"
          :aria-label="'Go to passage: ' + row.event.description"
          @click="
            emit('jump', {
              sectionIndex: row.event.sectionIndex,
              locationStart: row.event.locationStart,
            })
          "
        >
          <span class="block text-xs text-[var(--sm-muted)]">{{ row.section }} ↗</span>
          <span class="block py-1 text-sm">{{ row.event.description }}</span>
          <span class="block text-xs font-medium">{{ row.names }}</span>
        </button>
      </li>
    </ol>
  </div>
</template>

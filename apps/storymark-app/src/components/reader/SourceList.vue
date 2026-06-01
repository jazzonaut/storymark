<script setup lang="ts">
import type { AssistantSource, SectionSummary } from "@storymark/shared-types";

const props = defineProps<{
  sources: readonly AssistantSource[];
  manifest: readonly SectionSummary[];
  title: string;
  open: boolean;
}>();

defineEmits<{ (e: "select", source: AssistantSource): void }>();

function sectionLabel(sectionIndex: number): string {
  const title = props.manifest.find((s) => s.sectionIndex === sectionIndex)?.title;
  return title ?? `Section ${String(sectionIndex)}`;
}
</script>

<template>
  <details v-if="sources.length > 0" :open="open">
    <summary
      class="cursor-pointer list-none text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase select-none"
    >
      {{ title }} ({{ sources.length }})
    </summary>
    <ul class="mt-1.5 space-y-1.5">
      <li v-for="source in sources" :key="source.index">
        <button
          type="button"
          class="block w-full rounded-md border border-[var(--sm-muted)]/20 bg-[var(--sm-surface)] px-3 py-2 text-left hover:border-[var(--sm-accent)]"
          @click="$emit('select', source)"
        >
          <span class="flex items-baseline gap-1.5 text-xs font-medium text-[var(--sm-accent)]">
            <span class="font-semibold">{{ source.index }}.</span>
            {{ sectionLabel(source.sectionIndex) }}
          </span>
          <span class="mt-0.5 block text-sm text-[var(--sm-muted)]">{{ source.snippet }}</span>
        </button>
      </li>
    </ul>
  </details>
</template>

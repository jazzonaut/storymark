<script setup lang="ts">
import type { AssistantSource, SectionSummary } from "@storymark/shared-types";

const props = defineProps<{
  sources: readonly AssistantSource[];
  manifest: readonly SectionSummary[];
}>();

defineEmits<{ (e: "select", source: AssistantSource): void }>();

function sectionLabel(sectionIndex: number): string {
  const title = props.manifest.find((s) => s.sectionIndex === sectionIndex)?.title;
  return title ?? `Section ${String(sectionIndex)}`;
}
</script>

<template>
  <div v-if="sources.length > 0">
    <h3 class="mb-1.5 text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
      Sources
    </h3>
    <ul class="space-y-1.5">
      <li v-for="(source, i) in sources" :key="i">
        <button
          type="button"
          class="block w-full rounded-md border border-[var(--sm-muted)]/20 bg-[var(--sm-surface)] px-3 py-2 text-left hover:border-[var(--sm-accent)]"
          @click="$emit('select', source)"
        >
          <span class="block text-xs font-medium text-[var(--sm-accent)]">
            {{ sectionLabel(source.sectionIndex) }}
          </span>
          <span class="mt-0.5 block text-sm text-[var(--sm-muted)]">{{ source.snippet }}</span>
        </button>
      </li>
    </ul>
  </div>
</template>

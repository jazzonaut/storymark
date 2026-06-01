<script setup lang="ts">
import { nextTick, ref, toRef, watch } from "vue";
import type { SectionSummary } from "@storymark/shared-types";
import { useFocusTrap } from "@/composables/use-focus-trap";
import { sectionLabel } from "@/utils/section-label";

const props = defineProps<{
  open: boolean;
  manifest: readonly SectionSummary[];
  currentSectionIndex: number | null;
}>();

const emit = defineEmits<{
  (e: "close"): void;
  (e: "jump", sectionIndex: number): void;
}>();

const panel = ref<HTMLElement | null>(null);
useFocusTrap(toRef(props, "open"), panel);

const wordCount = new Intl.NumberFormat();

// Bring the current section into view each time the panel opens, so a reader deep
// into a long book doesn't land at the top of the list.
watch(
  () => props.open,
  (open) => {
    if (!open) {
      return;
    }
    void nextTick(() => {
      panel.value?.querySelector("[data-current='true']")?.scrollIntoView({ block: "center" });
    });
  },
);
</script>

<template>
  <div v-if="open" class="absolute inset-0 z-30" @click.self="emit('close')">
    <div class="absolute inset-0 bg-black/30"></div>
    <section
      ref="panel"
      role="dialog"
      aria-modal="true"
      aria-label="Contents"
      tabindex="-1"
      class="absolute inset-x-0 bottom-0 max-h-[70vh] overflow-y-auto rounded-t-2xl bg-[var(--sm-bg)] p-5 shadow-2xl outline-none sm:inset-y-0 sm:right-0 sm:left-auto sm:w-96 sm:max-h-none sm:rounded-none"
    >
      <div class="mb-4 flex items-center justify-between">
        <h2 class="text-base font-semibold">Contents</h2>
        <button
          type="button"
          class="rounded px-2 py-1 text-sm text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
          @click="emit('close')"
        >
          Done
        </button>
      </div>

      <p v-if="manifest.length === 0" class="text-sm text-[var(--sm-muted)]">
        This book has no sections to show.
      </p>

      <ul v-else class="space-y-0.5">
        <li v-for="section in manifest" :key="section.sectionIndex">
          <button
            type="button"
            :data-current="section.sectionIndex === currentSectionIndex"
            :aria-current="section.sectionIndex === currentSectionIndex ? 'true' : undefined"
            class="flex w-full items-baseline gap-3 rounded-md px-2 py-2 text-left hover:bg-[var(--sm-surface)] aria-[current]:bg-[var(--sm-surface)]"
            @click="emit('jump', section.sectionIndex)"
          >
            <span class="w-6 shrink-0 text-right text-xs tabular-nums text-[var(--sm-muted)]">
              {{ section.sectionIndex }}
            </span>
            <span
              class="min-w-0 flex-1 truncate text-sm"
              :class="section.sectionIndex === currentSectionIndex ? 'font-medium' : ''"
            >
              {{ sectionLabel(manifest, section.sectionIndex) }}
            </span>
            <span
              v-if="section.wordCount > 0"
              class="shrink-0 text-xs tabular-nums text-[var(--sm-muted)]"
            >
              {{ wordCount.format(section.wordCount) }} words
            </span>
          </button>
        </li>
      </ul>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import type { MemorySnapshot, SectionSummary } from "@storymark/shared-types";

const props = defineProps<{
  snapshot: MemorySnapshot;
  manifest: readonly SectionSummary[];
}>();

const emit = defineEmits<{
  (e: "dismiss"): void;
  (e: "dont-show-again"): void;
}>();

const boundaryLabel = computed<string>(() => {
  const title = props.manifest.find((s) => s.sectionIndex === props.snapshot.upToSection)?.title;
  return title ?? `section ${String(props.snapshot.upToSection)}`;
});

// Swipe-up to dismiss: track the vertical drag and dismiss past a small threshold.
const SWIPE_DISMISS_PX = 48;
const touchStartY = ref<number | null>(null);

function onTouchStart(event: TouchEvent): void {
  touchStartY.value = event.touches[0]?.clientY ?? null;
}

function onTouchEnd(event: TouchEvent): void {
  const start = touchStartY.value;
  const end = event.changedTouches[0]?.clientY;
  touchStartY.value = null;
  if (start !== null && end !== undefined && start - end > SWIPE_DISMISS_PX) {
    emit("dismiss");
  }
}
</script>

<template>
  <div class="absolute inset-0 z-30 flex items-start justify-center" @click.self="emit('dismiss')">
    <div class="absolute inset-0 bg-black/30" @click="emit('dismiss')"></div>

    <section
      class="relative mx-4 mt-[max(1rem,env(safe-area-inset-top))] max-h-[70vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-[var(--sm-bg)] p-5 shadow-2xl"
      @touchstart.passive="onTouchStart"
      @touchend.passive="onTouchEnd"
    >
      <div class="mx-auto mb-3 h-1 w-10 rounded-full bg-[var(--sm-muted)]/30"></div>

      <div class="mb-3 flex items-start justify-between gap-3">
        <div>
          <h2 class="text-base font-semibold">The story so far</h2>
          <p class="text-xs text-[var(--sm-muted)]">Up to {{ boundaryLabel }}</p>
        </div>
        <button
          type="button"
          class="-mt-1 -mr-1 shrink-0 rounded p-1 text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
          aria-label="Dismiss recap"
          @click="emit('dismiss')"
        >
          ✕
        </button>
      </div>

      <p class="text-sm leading-relaxed whitespace-pre-line text-[var(--sm-fg)]">
        {{ snapshot.summarySoFar }}
      </p>

      <div v-if="snapshot.openThreads.length > 0" class="mt-4">
        <h3 class="mb-1 text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
          Open threads
        </h3>
        <ul class="list-disc space-y-1 pl-5 text-sm text-[var(--sm-fg)]">
          <li v-for="(thread, i) in snapshot.openThreads" :key="i">{{ thread }}</li>
        </ul>
      </div>

      <div class="mt-5 flex items-center justify-between">
        <button
          type="button"
          class="text-xs text-[var(--sm-muted)] hover:text-[var(--sm-fg)] hover:underline"
          @click="emit('dont-show-again')"
        >
          Don't show again
        </button>
        <button
          type="button"
          class="rounded-md bg-[var(--sm-accent)] px-4 py-1.5 text-sm font-medium text-white hover:opacity-90"
          @click="emit('dismiss')"
        >
          Continue reading
        </button>
      </div>
    </section>
  </div>
</template>

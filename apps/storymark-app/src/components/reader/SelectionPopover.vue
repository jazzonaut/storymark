<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import EntityCard from "@/components/reader/EntityCard.vue";
import HintText from "@/components/reader/HintText.vue";
import { useSelectionHint } from "@/composables/use-selection-hint";
import type { ReaderSelection } from "@/composables/use-selection-hint";
import { useOfflineSync } from "@/composables/use-offline-sync";

const props = defineProps<{
  selection: ReaderSelection;
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
}>();

const emit = defineEmits<{
  (e: "close"): void;
  (e: "ask", text: string): void;
}>();

const { isResolving, result, error, resolve, reset } = useSelectionHint();
const { isOnline } = useOfflineSync();

const POPOVER_WIDTH = 288;
const GAP = 8;
const ABOVE_THRESHOLD = 180;

const root = ref<HTMLElement | null>(null);

const canIdentify = computed<boolean>(() => isOnline.value && props.currentSection !== null);

const style = computed<Record<string, string>>(() => {
  const rect = props.selection.rect;
  const viewportWidth = window.innerWidth;
  const left = Math.min(
    Math.max(rect.left + rect.width / 2 - POPOVER_WIDTH / 2, GAP),
    viewportWidth - POPOVER_WIDTH - GAP,
  );
  if (rect.top > ABOVE_THRESHOLD) {
    return {
      left: `${String(left)}px`,
      top: `${String(rect.top - GAP)}px`,
      transform: "translateY(-100%)",
    };
  }
  return { left: `${String(left)}px`, top: `${String(rect.bottom + GAP)}px` };
});

function identify(): void {
  if (props.currentSection === null) {
    return;
  }
  void resolve(props.bookId, props.selection.text, props.currentSection, props.currentLocation);
}

function onPointerDown(event: PointerEvent): void {
  if (event.target instanceof Node && root.value && !root.value.contains(event.target)) {
    emit("close");
  }
}

// A fresh selection starts back at the chips.
watch(() => props.selection.text, reset);

onMounted(() => {
  reset();
  document.addEventListener("pointerdown", onPointerDown, true);
});

onBeforeUnmount(() => {
  document.removeEventListener("pointerdown", onPointerDown, true);
});
</script>

<template>
  <div
    ref="root"
    class="fixed z-40 w-72 rounded-xl border border-[var(--sm-muted)]/20 bg-[var(--sm-bg)] p-3 shadow-2xl"
    :style="style"
  >
    <div v-if="isResolving" class="space-y-2" aria-busy="true">
      <div class="h-3 w-2/3 animate-pulse rounded bg-[var(--sm-surface)]"></div>
      <div class="h-3 w-full animate-pulse rounded bg-[var(--sm-surface)]"></div>
    </div>

    <p v-else-if="error" class="text-sm text-red-600">{{ error }}</p>

    <EntityCard v-else-if="result?.kind === 'entity'" :card="result.card" />

    <HintText
      v-else-if="result?.kind === 'hint'"
      :hint="result.hint"
      @tell-me-more="emit('ask', selection.text)"
    />

    <div v-else class="flex flex-wrap gap-1.5">
      <button
        type="button"
        :disabled="!canIdentify"
        class="rounded-full bg-[var(--sm-surface)] px-3 py-1 text-xs font-medium text-[var(--sm-fg)] hover:bg-[var(--sm-accent)] hover:text-white disabled:opacity-50 disabled:hover:bg-[var(--sm-surface)] disabled:hover:text-[var(--sm-fg)]"
        @click="identify"
      >
        Who/What is this?
      </button>
      <button
        type="button"
        disabled
        class="cursor-not-allowed rounded-full bg-[var(--sm-surface)] px-3 py-1 text-xs font-medium text-[var(--sm-muted)] opacity-50"
        title="Available in a later update"
      >
        Recap to here
      </button>
      <button
        type="button"
        class="rounded-full bg-[var(--sm-surface)] px-3 py-1 text-xs font-medium text-[var(--sm-fg)] hover:bg-[var(--sm-accent)] hover:text-white"
        @click="emit('ask', selection.text)"
      >
        Ask…
      </button>
    </div>
  </div>
</template>

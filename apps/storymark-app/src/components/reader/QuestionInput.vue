<script setup lang="ts">
import { useAssistant } from "@/composables/use-assistant";

const props = defineProps<{ disabled: boolean; pending: boolean; followUp: boolean }>();

const emit = defineEmits<{ (e: "ask", question: string): void }>();

// Shared so startSelectionAsk/reset can clear a typed-but-unsent question.
const { draft } = useAssistant();

function submit(): void {
  const value = draft.value.trim();
  if (value.length === 0 || props.disabled || props.pending) {
    return;
  }
  emit("ask", value);
  draft.value = "";
}
</script>

<template>
  <form class="flex gap-2" @submit.prevent="submit">
    <input
      v-model="draft"
      type="text"
      :disabled="disabled"
      :placeholder="
        disabled
          ? 'Q&A is unavailable offline'
          : followUp
            ? 'Ask a follow-up…'
            : 'Ask about this book…'
      "
      class="min-w-0 flex-1 rounded-md border border-[var(--sm-muted)]/30 bg-[var(--sm-surface)] px-3 py-2 text-sm text-[var(--sm-fg)] placeholder:text-[var(--sm-muted)] focus:border-[var(--sm-accent)] focus:outline-none disabled:opacity-60"
    />
    <button
      type="submit"
      :disabled="disabled || pending"
      class="shrink-0 rounded-md bg-[var(--sm-accent)] px-4 py-2 text-sm font-medium text-white hover:opacity-90 disabled:opacity-50"
    >
      {{ pending ? "…" : "Ask" }}
    </button>
  </form>
</template>

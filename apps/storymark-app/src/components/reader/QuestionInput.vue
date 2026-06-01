<script setup lang="ts">
import { ref } from "vue";

const props = defineProps<{ disabled: boolean; pending: boolean }>();

const emit = defineEmits<{ (e: "ask", question: string): void }>();

const question = ref("");

function submit(): void {
  const value = question.value.trim();
  if (value.length === 0 || props.disabled || props.pending) {
    return;
  }
  emit("ask", value);
  question.value = "";
}
</script>

<template>
  <form class="flex gap-2" @submit.prevent="submit">
    <input
      v-model="question"
      type="text"
      :disabled="disabled"
      :placeholder="disabled ? 'Q&A is unavailable offline' : 'Ask about this book…'"
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

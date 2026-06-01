<script setup lang="ts">
import { computed, ref } from "vue";
import type { BookSummary } from "@storymark/shared-types";

const props = defineProps<{ book: BookSummary; busy: boolean; error: string | null }>();

const emit = defineEmits<{
  (e: "cancel"): void;
  (e: "confirm"): void;
}>();

const typed = ref("");
const canDelete = computed<boolean>(() => typed.value.trim() === props.book.title && !props.busy);

function onConfirm(): void {
  if (canDelete.value) {
    emit("confirm");
  }
}
</script>

<template>
  <div
    class="fixed inset-0 z-50 flex items-center justify-center p-4"
    role="dialog"
    aria-modal="true"
    @click.self="!busy && emit('cancel')"
  >
    <div class="absolute inset-0 bg-black/40"></div>
    <section
      class="relative w-full max-w-md rounded-2xl bg-[var(--sm-bg)] p-6 shadow-2xl"
    >
      <h2 class="text-lg font-semibold">Delete this book?</h2>
      <p class="mt-2 text-sm text-[var(--sm-muted)]">
        This permanently removes <span class="font-medium text-[var(--sm-fg)]">{{ book.title }}</span>
        and all of its data — sections, bookmarks, reading progress, and everything Storymark
        extracted. This cannot be undone.
      </p>

      <label class="mt-4 block text-sm font-medium">
        Type the title to confirm
        <input
          v-model="typed"
          type="text"
          autocomplete="off"
          :placeholder="book.title"
          :disabled="busy"
          class="mt-1 w-full rounded-md border border-[var(--sm-muted)]/40 bg-transparent px-3 py-2 text-sm outline-none focus:border-[var(--sm-accent)]"
          @keydown.enter="onConfirm"
        />
      </label>

      <p v-if="error" class="mt-2 text-sm text-red-700 dark:text-red-300">{{ error }}</p>

      <div class="mt-5 flex justify-end gap-2">
        <button
          type="button"
          class="rounded-md px-4 py-2 text-sm font-medium text-[var(--sm-muted)] hover:text-[var(--sm-fg)] disabled:opacity-50"
          :disabled="busy"
          @click="emit('cancel')"
        >
          Cancel
        </button>
        <button
          type="button"
          class="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
          :disabled="!canDelete"
          @click="onConfirm"
        >
          {{ busy ? "Deleting…" : "Delete" }}
        </button>
      </div>
    </section>
  </div>
</template>

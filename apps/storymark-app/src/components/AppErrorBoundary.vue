<script setup lang="ts">
import { onErrorCaptured, ref } from "vue";

// Without this, an uncaught error thrown during a child's render or lifecycle
// propagates to the root and unmounts the whole tree — a blank screen with no way
// back, which is especially bad for an installed offline PWA. Catching it here
// swaps in a recoverable fallback instead. We deliberately don't return `false`:
// letting the error propagate keeps Vue's own dev logging intact (this codebase
// forbids `console`, so that built-in path is the report), while the fallback
// still renders because `failed` is already set.
const failed = ref(false);

onErrorCaptured(() => {
  failed.value = true;
});

function reload(): void {
  window.location.reload();
}
</script>

<template>
  <div
    v-if="failed"
    role="alert"
    class="flex min-h-dvh flex-col items-center justify-center gap-4 p-6 text-center"
  >
    <div>
      <p class="text-lg font-semibold">Something went wrong.</p>
      <p class="mt-1 text-sm text-[var(--sm-muted)]">
        The app hit an unexpected error. Your reading progress is saved.
      </p>
    </div>
    <div class="flex items-center gap-4">
      <button
        type="button"
        class="rounded-md bg-[var(--sm-accent)] px-4 py-2 text-sm font-medium text-white hover:opacity-90"
        @click="reload"
      >
        Reload
      </button>
      <a href="/" class="text-sm text-[var(--sm-muted)] hover:underline">Back to library</a>
    </div>
  </div>
  <slot v-else />
</template>

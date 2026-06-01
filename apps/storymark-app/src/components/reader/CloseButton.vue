<script setup lang="ts">
defineEmits<{ (e: "close"): void }>();
</script>

<template>
  <button
    type="button"
    class="shrink-0 rounded px-2 py-1 text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
    aria-label="Close"
    @click="$emit('close')"
  >
    <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="2">
      <path d="M6 6 18 18M18 6 6 18" stroke-linecap="round" stroke-linejoin="round" />
    </svg>
  </button>
</template>

<script setup lang="ts">
defineProps<{ active: boolean }>();

defineEmits<{ (e: "toggle"): void }>();
</script>

<template>
  <button
    type="button"
    class="shrink-0 rounded px-2 py-1 text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
    :class="{ 'text-[var(--sm-accent)]': active }"
    :aria-pressed="active"
    :aria-label="active ? 'Remove bookmark' : 'Bookmark this page'"
    @click="$emit('toggle')"
  >
    <svg
      viewBox="0 0 24 24"
      class="h-5 w-5"
      :fill="active ? 'currentColor' : 'none'"
      stroke="currentColor"
      stroke-width="1.8"
    >
      <path d="M6 3h12a1 1 0 0 1 1 1v17l-7-4-7 4V4a1 1 0 0 1 1-1z" stroke-linejoin="round" />
    </svg>
  </button>
</template>

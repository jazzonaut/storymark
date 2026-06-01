<script setup lang="ts">
defineProps<{ type: string }>();
</script>

<template>
  <span
    class="rounded-full bg-[var(--sm-surface)] px-2 py-0.5 text-xs font-medium text-[var(--sm-muted)]"
  >
    {{ type }}
  </span>
</template>

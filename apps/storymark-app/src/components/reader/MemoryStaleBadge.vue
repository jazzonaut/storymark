<script setup lang="ts">
// Shown above a Memory tab when it is rendering a saved copy because the live data
// couldn't be refreshed (API unreachable / offline). Driven by the read-through
// cache's "stale-offline" status, so stale data is never presented as current.
</script>

<template>
  <p class="flex items-center gap-1.5 text-xs text-[var(--sm-muted)]">
    <span class="inline-block h-1.5 w-1.5 rounded-full bg-amber-500"></span>
    Offline — showing the last saved copy.
  </p>
</template>

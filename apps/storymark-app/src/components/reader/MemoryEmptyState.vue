<script setup lang="ts">
// Shared empty state for the Memory panel's data tabs: while the book is still
// being processed the data genuinely isn't ready yet, so say so rather than
// implying there's nothing to show.
defineProps<{ processing: boolean; idle: string }>();
</script>

<template>
  <p class="text-sm text-[var(--sm-muted)]">
    {{ processing ? "Still processing this book — check back in a minute." : idle }}
  </p>
</template>

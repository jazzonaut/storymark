<script setup lang="ts">
import type { MemoryTab } from "@/composables/use-memory-panel";

defineProps<{ tab: MemoryTab }>();
</script>

<template>
  <svg
    viewBox="0 0 24 24"
    class="h-4 w-4 shrink-0"
    fill="none"
    stroke="currentColor"
    stroke-width="1.8"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <!-- Summary: lines of text -->
    <template v-if="tab === 'summary'">
      <path d="M4 6h16M4 11h16M4 16h10" />
    </template>
    <!-- Timeline: events along an axis -->
    <template v-else-if="tab === 'timeline'">
      <circle cx="5" cy="12" r="1.8" />
      <circle cx="12" cy="12" r="1.8" />
      <circle cx="19" cy="12" r="1.8" />
      <path d="M6.8 12h3.4M13.8 12h3.4" />
    </template>
    <!-- Graph: connected nodes -->
    <template v-else-if="tab === 'graph'">
      <circle cx="18" cy="5" r="2.5" />
      <circle cx="6" cy="12" r="2.5" />
      <circle cx="18" cy="19" r="2.5" />
      <path d="M8.3 13.3l7.4 4.4M15.7 6.3 8.3 10.7" />
    </template>
    <!-- Story Guide: compass -->
    <template v-else-if="tab === 'guide'">
      <circle cx="12" cy="12" r="9" />
      <path d="M16.2 7.8l-2.1 6.3-6.3 2.1 2.1-6.3z" />
    </template>
    <!-- Assistant: speech bubble -->
    <template v-else-if="tab === 'assistant'">
      <path
        d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8z"
      />
    </template>
    <!-- Scene: eye on the current moment -->
    <template v-else>
      <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7-10-7-10-7z" />
      <circle cx="12" cy="12" r="3" />
    </template>
  </svg>
</template>

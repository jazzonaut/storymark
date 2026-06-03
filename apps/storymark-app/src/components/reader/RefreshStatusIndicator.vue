<script setup lang="ts">
// Shown above (or beside) a cached surface to flag how its data relates to the
// live copy: a refresh is in flight over already-shown cached data, or a refresh
// failed and the saved copy is being shown instead. Driven by the read-through
// cache's fallback reason, so stale data is never presented as current — an
// ahead-stamped copy (ADR-0022) is called out explicitly.
// Renders nothing when the data is fresh and no refresh is running.
import { computed } from "vue";
import type { CacheFallback } from "@/api/read-through-cache";

const props = defineProps<{
  refreshing: boolean;
  fallback: CacheFallback | null;
}>();

const copyLabel = computed<string>(() =>
  props.fallback?.ahead === true
    ? "showing a saved copy from a little further ahead."
    : "showing the last saved copy.",
);
</script>

<template>
  <p
    v-if="refreshing"
    class="flex items-center gap-1.5 text-xs text-[var(--sm-muted)]"
    aria-live="polite"
  >
    <span
      class="inline-block h-3 w-3 animate-spin rounded-full border border-[var(--sm-muted)]/30 border-t-[var(--sm-muted)]"
    ></span>
    Updating…
  </p>
  <p
    v-else-if="fallback?.kind === 'offline'"
    class="flex items-center gap-1.5 text-xs text-[var(--sm-muted)]"
  >
    <span class="inline-block h-1.5 w-1.5 rounded-full bg-amber-500"></span>
    Offline — {{ copyLabel }}
  </p>
  <p
    v-else-if="fallback?.kind === 'http'"
    class="flex items-center gap-1.5 text-xs text-[var(--sm-muted)]"
  >
    <svg
      class="h-3.5 w-3.5 shrink-0 text-amber-500"
      viewBox="0 0 16 16"
      fill="currentColor"
      aria-hidden="true"
    >
      <path
        d="M8 1.5 0.5 14.5h15L8 1.5Zm0 4a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 8 5.5Zm0 6.5a.9.9 0 1 1 0 1.8.9.9 0 0 1 0-1.8Z"
      />
    </svg>
    Couldn't refresh ({{ fallback.status }}) — {{ copyLabel }}
  </p>
</template>

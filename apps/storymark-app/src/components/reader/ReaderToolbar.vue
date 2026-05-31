<script setup lang="ts">
import { RouterLink } from "vue-router";

defineProps<{ title: string; sectionTitle: string | null; position: string; visible: boolean }>();

defineEmits<{ (e: "open-settings"): void }>();
</script>

<template>
  <header
    class="absolute inset-x-0 top-0 z-20 flex items-center gap-3 border-b border-[var(--sm-muted)]/20 bg-[var(--sm-bg)]/95 px-4 py-3 backdrop-blur transition-transform duration-200"
    :class="visible ? 'translate-y-0' : '-translate-y-full'"
  >
    <RouterLink
      to="/"
      class="shrink-0 rounded px-2 py-1 text-sm text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
      aria-label="Back to library"
    >
      ‹ Library
    </RouterLink>
    <div class="min-w-0 flex-1 text-center">
      <p class="truncate text-sm font-medium" :title="title">{{ title }}</p>
      <p class="truncate text-xs text-[var(--sm-muted)]">
        {{ sectionTitle ?? "Untitled section" }} · {{ position }}
      </p>
    </div>
    <button
      type="button"
      class="shrink-0 rounded px-2 py-1 text-sm text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
      aria-label="Reader settings"
      @click="$emit('open-settings')"
    >
      Aa
    </button>
  </header>
</template>

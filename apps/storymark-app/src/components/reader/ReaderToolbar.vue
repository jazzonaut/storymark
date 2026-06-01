<script setup lang="ts">
import { RouterLink } from "vue-router";
import BookmarkButton from "@/components/reader/BookmarkButton.vue";

defineProps<{
  title: string;
  sectionTitle: string | null;
  bookPercent: string;
  timeLeft: string;
  visible: boolean;
  offline: boolean;
  bookmarked: boolean;
}>();

defineEmits<{
  (e: "open-settings"): void;
  (e: "open-bookmarks"): void;
  (e: "open-memory"): void;
  (e: "open-contents"): void;
  (e: "toggle-bookmark"): void;
}>();
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
    <button
      type="button"
      class="min-w-0 flex-1 rounded px-2 py-1 text-center hover:bg-[var(--sm-surface)]"
      aria-label="Open contents"
      @click="$emit('open-contents')"
    >
      <span class="block truncate text-sm font-medium" :title="title">{{ title }}</span>
      <span class="flex min-w-0 items-baseline justify-center gap-1 text-xs text-[var(--sm-muted)]">
        <span class="min-w-0 truncate">{{ sectionTitle ?? "Untitled section" }}</span>
        <span class="shrink-0 whitespace-nowrap">· {{ bookPercent }} · {{ timeLeft }}</span>
      </span>
    </button>
    <span
      v-if="offline"
      class="shrink-0 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-900 dark:text-amber-200"
    >
      Offline
    </span>
    <BookmarkButton :active="bookmarked" @toggle="$emit('toggle-bookmark')" />
    <button
      type="button"
      class="shrink-0 rounded px-2 py-1 text-sm text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
      aria-label="Open the Memory panel"
      @click="$emit('open-memory')"
    >
      Memory
    </button>
    <button
      type="button"
      class="shrink-0 rounded px-2 py-1 text-sm text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
      aria-label="Bookmarks"
      @click="$emit('open-bookmarks')"
    >
      ☰
    </button>
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

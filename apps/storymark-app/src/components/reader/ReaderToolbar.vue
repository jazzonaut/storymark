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
      class="shrink-0 rounded px-2 py-1 text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
      aria-label="Open the Memory panel"
      @click="$emit('open-memory')"
    >
      <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="1.8">
        <path
          d="M11 9C9.3 8 6.9 7.7 3.8 7.7 3.3 7.7 2.9 7.7 2.5 7.8V18.3C2.9 18.2 3.3 18.2 3.8 18.2 6.9 18.2 9.3 18.5 11 19.5M11 9C12.7 8 15.1 7.7 18.2 7.7 18.7 7.7 19.1 7.7 19.5 7.8V18.3C19.1 18.2 18.7 18.2 18.2 18.2 15.1 18.2 12.7 18.5 11 19.5M11 9V19.5"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
        <path
          d="M17.5 1.5C17.7 2.9 18.6 3.8 20 4 18.6 4.2 17.7 5.1 17.5 6.5 17.3 5.1 16.4 4.2 15 4 16.4 3.8 17.3 2.9 17.5 1.5Z"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
      </svg>
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

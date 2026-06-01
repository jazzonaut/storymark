<script setup lang="ts">
import { computed, ref, toRef } from "vue";
import type { SectionSummary } from "@storymark/shared-types";
import type { StoredBookmark } from "@/db/storymark-db";
import { useFocusTrap } from "@/composables/use-focus-trap";
import BookmarkButton from "@/components/reader/BookmarkButton.vue";

const props = defineProps<{
  open: boolean;
  bookmarks: readonly StoredBookmark[];
  manifest: readonly SectionSummary[];
  currentBookmarked: boolean;
}>();

const panel = ref<HTMLElement | null>(null);
useFocusTrap(toRef(props, "open"), panel);

defineEmits<{
  (e: "close"): void;
  (e: "jump", bookmark: StoredBookmark): void;
  (e: "remove", id: string): void;
  (e: "toggle-bookmark"): void;
}>();

interface BookmarkGroup {
  readonly sectionIndex: number;
  readonly title: string;
  readonly items: readonly StoredBookmark[];
}

const groups = computed<readonly BookmarkGroup[]>(() => {
  const titleOf = new Map(props.manifest.map((s) => [s.sectionIndex, s.title]));
  const bySection = new Map<number, StoredBookmark[]>();
  for (const bookmark of props.bookmarks) {
    const list = bySection.get(bookmark.sectionIndex);
    if (list) {
      list.push(bookmark);
    } else {
      bySection.set(bookmark.sectionIndex, [bookmark]);
    }
  }
  return [...bySection.entries()]
    .map(([sectionIndex, items]) => ({
      sectionIndex,
      title: titleOf.get(sectionIndex) ?? "Untitled section",
      items,
    }))
    .toSorted((a, b) => a.sectionIndex - b.sectionIndex);
});
</script>

<template>
  <div v-if="open" class="absolute inset-0 z-30">
    <div class="absolute inset-0 bg-black/30" @click="$emit('close')"></div>
    <section
      ref="panel"
      role="dialog"
      aria-modal="true"
      aria-label="Bookmarks"
      tabindex="-1"
      class="absolute inset-x-0 bottom-0 max-h-[70vh] overflow-y-auto rounded-t-2xl bg-[var(--sm-bg)] p-5 shadow-2xl outline-none sm:inset-y-0 sm:right-0 sm:left-auto sm:w-96 sm:max-h-none sm:rounded-none"
    >
      <div class="mb-4 flex items-center justify-between">
        <h2 class="text-base font-semibold">Bookmarks</h2>
        <div class="flex items-center gap-1">
          <BookmarkButton :active="currentBookmarked" @toggle="$emit('toggle-bookmark')" />
          <button
            type="button"
            class="rounded px-2 py-1 text-sm text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
            @click="$emit('close')"
          >
            Done
          </button>
        </div>
      </div>

      <p v-if="groups.length === 0" class="text-sm text-[var(--sm-muted)]">
        No bookmarks yet. Tap the bookmark icon above to save your current spot.
      </p>

      <div v-for="group in groups" :key="group.sectionIndex" class="mb-4">
        <h3 class="mb-1 text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
          {{ group.title }}
        </h3>
        <ul class="space-y-1">
          <li
            v-for="bookmark in group.items"
            :key="bookmark.id"
            class="flex items-start gap-2 rounded-md px-2 py-1.5 hover:bg-[var(--sm-surface)]"
          >
            <button
              type="button"
              class="min-w-0 flex-1 text-left text-sm"
              @click="$emit('jump', bookmark)"
            >
              {{ bookmark.label ?? `Paragraph ${String(bookmark.paragraphIndex + 1)}` }}
            </button>
            <button
              type="button"
              class="shrink-0 rounded px-1 text-[var(--sm-muted)] hover:text-red-600"
              aria-label="Delete bookmark"
              @click="$emit('remove', bookmark.id)"
            >
              ✕
            </button>
          </li>
        </ul>
      </div>
    </section>
  </div>
</template>

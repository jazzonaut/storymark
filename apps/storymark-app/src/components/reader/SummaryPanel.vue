<script setup lang="ts">
import { ref, watch } from "vue";
import type { MemorySnapshot } from "@storymark/shared-types";
import MemoryEmptyState from "@/components/reader/MemoryEmptyState.vue";
import RefreshStatusIndicator from "@/components/reader/RefreshStatusIndicator.vue";
import { memoryApi } from "@/api/memory-api";
import { describeLoadError, useCachedLoad } from "@/composables/use-cached-load";

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  cacheVersion: string | null;
  isProcessing: boolean;
}>();

const snapshot = ref<MemorySnapshot | null>(null);
const loader = useCachedLoad();
// Destructured so the template's top-level bindings unwrap the refs.
const { isLoading, isRefreshing, error, fallback } = loader;

async function load(): Promise<void> {
  if (props.currentSection === null) {
    snapshot.value = null;
    return;
  }
  const section = props.currentSection;
  await loader.run({
    load: async (ctx) => {
      // The story so far up to the start of the current section (the latest snapshot
      // strictly before it). That at-or-before snapshot is stable for any position
      // within the section, so this reuses the section-entry recap's read-through
      // cache — the summary renders offline once seen, and a reprocess (cacheVersion)
      // invalidates it (showing the prior copy with a refreshing badge while it refetches).
      const result = await memoryApi.getForSectionEntry(
        props.bookId,
        section,
        props.cacheVersion,
        !props.isProcessing,
        ctx.showCached((cached) => {
          snapshot.value = cached;
        }),
      );
      if (!ctx.isCurrent()) {
        return;
      }
      snapshot.value = result.value;
      ctx.commit(result);
    },
    describeError: describeLoadError(
      "Couldn't load the summary just now.",
      "The summary is unavailable offline.",
    ),
    onFailure: () => {
      snapshot.value = null;
    },
  });
}

// The section-entry snapshot is keyed by section (location-independent), so reload on
// section and on a reprocess (cacheVersion), not on every in-section move.
watch(
  () => [props.bookId, props.currentSection, props.cacheVersion] as const,
  () => {
    void load();
  },
  { immediate: true },
);
</script>

<template>
  <div class="flex h-full flex-col gap-3">
    <div v-if="isLoading" class="h-24 w-full animate-pulse rounded-lg bg-[var(--sm-surface)]"></div>

    <p v-else-if="error" class="text-sm text-[var(--sm-muted)]">{{ error }}</p>

    <MemoryEmptyState
      v-else-if="snapshot === null"
      :processing="isProcessing"
      idle="The story so far will appear here as you read on."
    />

    <template v-else>
      <RefreshStatusIndicator :refreshing="isRefreshing" :fallback="fallback" />
      <p class="text-sm leading-relaxed whitespace-pre-line text-[var(--sm-fg)]">
        {{ snapshot.summarySoFar }}
      </p>
      <div v-if="snapshot.openThreads.length > 0">
        <h3 class="mb-1 text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
          Open threads
        </h3>
        <ul class="list-disc space-y-1 pl-5 text-sm text-[var(--sm-fg)]">
          <li v-for="(thread, i) in snapshot.openThreads" :key="i">{{ thread }}</li>
        </ul>
      </div>
    </template>
  </div>
</template>

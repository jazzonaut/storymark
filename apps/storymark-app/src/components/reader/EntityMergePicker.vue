<script setup lang="ts">
import { computed, ref, watch } from "vue";
import type { StoryGuideEntity } from "@storymark/shared-types";
import CloseButton from "@/components/reader/CloseButton.vue";
import EntityTypeBadge from "@/components/reader/EntityTypeBadge.vue";
import { entitiesApi } from "@/api/entities-api";
import { useEntityMerge } from "@/composables/use-entity-merge";
import { useFocusTrap } from "@/composables/use-focus-trap";
import { useLatestOnly } from "@/composables/use-latest-only";
import { ApiError } from "@/api/api-error";

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  cacheVersion: string | null;
  isProcessing: boolean;
}>();

const { survivor, cancelMerge, notifyMerged } = useEntityMerge();

const panel = ref<HTMLElement | null>(null);
const isOpen = computed<boolean>(() => survivor.value !== null);
useFocusTrap(isOpen, panel, cancelMerge);

const candidates = ref<readonly StoryGuideEntity[]>([]);
const selected = ref<Set<string>>(new Set());
const search = ref("");
const isLoading = ref(false);
const isMerging = ref(false);
const error = ref<string | null>(null);
const latest = useLatestOnly();

// Everything the reader has met except the survivor itself — those are the rows that can be
// folded in. Filtered live by the search box against the canonical name and any alias.
const filtered = computed<readonly StoryGuideEntity[]>(() => {
  const survivorId = survivor.value?.entityId;
  const query = search.value.trim().toLowerCase();
  return candidates.value.filter((entity) => {
    if (entity.entityId === survivorId) {
      return false;
    }
    if (query.length === 0) {
      return true;
    }
    return (
      entity.canonicalName.toLowerCase().includes(query) ||
      entity.aliases.some((alias) => alias.toLowerCase().includes(query))
    );
  });
});

const canMerge = computed<boolean>(() => selected.value.size > 0 && !isMerging.value);

function toggle(entityId: string): void {
  const next = new Set(selected.value);
  if (next.has(entityId)) {
    next.delete(entityId);
  } else {
    next.add(entityId);
  }
  selected.value = next;
}

async function loadCandidates(): Promise<void> {
  if (props.currentSection === null) {
    candidates.value = [];
    return;
  }
  const token = latest.begin();
  isLoading.value = true;
  error.value = null;
  try {
    const result = await entitiesApi.list(
      props.bookId,
      props.currentSection,
      props.currentLocation,
      props.cacheVersion,
      !props.isProcessing,
    );
    if (latest.isCurrent(token)) {
      candidates.value = result.value;
    }
  } catch (caught) {
    if (latest.isCurrent(token)) {
      error.value =
        caught instanceof ApiError
          ? "Couldn't load entities to merge."
          : "Merging is unavailable offline.";
      candidates.value = [];
    }
  } finally {
    if (latest.isCurrent(token)) {
      isLoading.value = false;
    }
  }
}

async function confirmMerge(): Promise<void> {
  const target = survivor.value;
  if (target === null || selected.value.size === 0 || isMerging.value) {
    return;
  }
  isMerging.value = true;
  error.value = null;
  try {
    await entitiesApi.merge(props.bookId, target.entityId, [...selected.value]);
    notifyMerged();
    cancelMerge();
  } catch (caught) {
    error.value =
      caught instanceof ApiError
        ? "The merge couldn't be completed just now."
        : "Merging is unavailable offline.";
  } finally {
    isMerging.value = false;
  }
}

// Reset and fetch fresh each time a new survivor is chosen; clear when dismissed.
watch(survivor, (value) => {
  selected.value = new Set();
  search.value = "";
  error.value = null;
  isMerging.value = false;
  if (value !== null) {
    void loadCandidates();
  }
});
</script>

<template>
  <div v-if="survivor" class="absolute inset-0 z-50">
    <div class="absolute inset-0 bg-black/30" @click="cancelMerge"></div>
    <section
      ref="panel"
      role="dialog"
      aria-modal="true"
      aria-labelledby="entity-merge-heading"
      tabindex="-1"
      class="absolute inset-x-0 bottom-0 flex max-h-[80vh] flex-col rounded-t-2xl bg-[var(--sm-bg)] shadow-2xl outline-none sm:inset-x-auto sm:top-1/2 sm:left-1/2 sm:max-h-[70vh] sm:w-[28rem] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-2xl"
    >
      <header
        class="flex items-center justify-between gap-3 border-b border-[var(--sm-muted)]/15 px-5 py-3"
      >
        <h2
          id="entity-merge-heading"
          class="min-w-0 truncate text-base font-semibold text-[var(--sm-fg)]"
        >
          Merge into {{ survivor.name }}
        </h2>
        <CloseButton @close="cancelMerge" />
      </header>

      <div class="border-b border-[var(--sm-muted)]/15 px-5 py-2">
        <p class="text-xs text-[var(--sm-muted)]">
          Pick the duplicate entries to fold into
          <span class="font-medium text-[var(--sm-fg)]">{{ survivor.name }}</span
          >. They become aliases; their summaries, mentions, and relationships move across.
        </p>
        <input
          v-model="search"
          type="search"
          placeholder="Search entities…"
          class="mt-2 w-full rounded-lg border border-[var(--sm-muted)]/20 bg-[var(--sm-surface)] px-3 py-2 text-sm text-[var(--sm-fg)] placeholder:text-[var(--sm-muted)] focus:outline-none"
        />
      </div>

      <div class="min-h-0 flex-1 overflow-y-auto px-5 py-3">
        <div v-if="isLoading" class="space-y-2" aria-busy="true">
          <div
            v-for="n in 4"
            :key="n"
            class="h-10 animate-pulse rounded-lg bg-[var(--sm-surface)]"
          ></div>
        </div>

        <p v-else-if="error !== null" class="text-sm text-[var(--sm-muted)]">{{ error }}</p>

        <p v-else-if="filtered.length === 0" class="text-sm text-[var(--sm-muted)]">
          No other entities to merge here.
        </p>

        <ul v-else class="space-y-1.5">
          <li v-for="entity in filtered" :key="entity.entityId">
            <label
              class="flex cursor-pointer items-center gap-3 rounded-lg border border-[var(--sm-muted)]/15 bg-[var(--sm-surface)]/40 px-3 py-2 hover:bg-[var(--sm-surface)]"
            >
              <input
                type="checkbox"
                class="size-4 shrink-0 accent-[var(--sm-accent)]"
                :checked="selected.has(entity.entityId)"
                @change="toggle(entity.entityId)"
              />
              <span class="flex min-w-0 flex-col">
                <span class="flex items-baseline gap-2">
                  <span class="truncate text-sm font-medium text-[var(--sm-fg)]">{{
                    entity.canonicalName
                  }}</span>
                  <EntityTypeBadge :type="entity.entityType" />
                </span>
                <span
                  v-if="entity.aliases.length > 0"
                  class="truncate text-[11px] text-[var(--sm-muted)]/80"
                >
                  also known as {{ entity.aliases.join(", ") }}
                </span>
              </span>
            </label>
          </li>
        </ul>
      </div>

      <footer
        class="flex items-center justify-end gap-2 border-t border-[var(--sm-muted)]/15 px-5 py-3"
      >
        <button
          type="button"
          class="rounded-lg px-3 py-2 text-sm font-medium text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
          @click="cancelMerge"
        >
          Cancel
        </button>
        <button
          type="button"
          class="rounded-lg bg-[var(--sm-accent)] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          :disabled="!canMerge"
          @click="confirmMerge"
        >
          {{ isMerging ? "Merging…" : `Merge ${String(selected.size)} in` }}
        </button>
      </footer>
    </section>
  </div>
</template>

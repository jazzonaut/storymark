<script setup lang="ts">
import { computed, ref, watch } from "vue";
import type { SectionSummary, StoryGuideEntity } from "@storymark/shared-types";
import EntityTypeBadge from "@/components/reader/EntityTypeBadge.vue";
import MemoryEmptyState from "@/components/reader/MemoryEmptyState.vue";
import MemoryStaleBadge from "@/components/reader/MemoryStaleBadge.vue";
import { entitiesApi } from "@/api/entities-api";
import { useEntitySummary } from "@/composables/use-entity-summary";
import { useEntityMerge } from "@/composables/use-entity-merge";
import { useLatestOnly } from "@/composables/use-latest-only";
import { ApiError } from "@/api/api-error";
import { entityTypeOrder, entityTypePlural } from "@/utils/entity-type";
import { sectionLabel } from "@/utils/section-label";

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  cacheVersion: string | null;
  manifest: readonly SectionSummary[];
  isProcessing: boolean;
}>();

const { open } = useEntitySummary();
const { mergeRevision } = useEntityMerge();

const entities = ref<readonly StoryGuideEntity[]>([]);
const isLoading = ref(false);
const error = ref<string | null>(null);
const isStale = ref(false);
const search = ref("");
const activeType = ref<string | null>(null);
// A slow earlier fetch must not overwrite the current boundary's list, so each load
// commits only if it is still the latest.
const latest = useLatestOnly();

// The entity types actually present, in the backend's declared order — the type
// filter only offers kinds the reader has met, so a chip is never a dead end.
const presentTypes = computed<readonly string[]>(() =>
  [...new Set(entities.value.map((entity) => entity.entityType))].toSorted(
    (a, b) => entityTypeOrder(a) - entityTypeOrder(b),
  ),
);

const filtered = computed<readonly StoryGuideEntity[]>(() => {
  const query = search.value.trim().toLowerCase();
  return entities.value
    .filter((entity) => {
      if (activeType.value !== null && entity.entityType !== activeType.value) {
        return false;
      }
      if (query.length === 0) {
        return true;
      }
      return (
        entity.canonicalName.toLowerCase().includes(query) ||
        entity.aliases.some((alias) => alias.toLowerCase().includes(query))
      );
    })
    .toSorted((a, b) => {
      const sectionDiff = (b.firstSeenSection ?? -1) - (a.firstSeenSection ?? -1);
      return sectionDiff !== 0 ? sectionDiff : a.canonicalName.localeCompare(b.canonicalName);
    });
});

// The secondary line under each entry: where it was introduced. Aliases are withheld
// for now — they aren't position-gated, so a not-yet-reached alias would be a spoiler.
function metaLine(entity: StoryGuideEntity): string {
  const parts: string[] = [];
  if (entity.firstSeenSection !== null) {
    parts.push(`Introduced in ${sectionLabel(props.manifest, entity.firstSeenSection)}`);
  }
  return parts.join(" · ");
}

async function load(): Promise<void> {
  if (props.currentSection === null) {
    entities.value = [];
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
    if (!latest.isCurrent(token)) {
      return;
    }
    entities.value = result.value;
    isStale.value = result.status === "stale-offline";
  } catch (caught) {
    if (!latest.isCurrent(token)) {
      return;
    }
    error.value =
      caught instanceof ApiError
        ? "Couldn't load the Story Guide just now."
        : "The Story Guide is unavailable offline.";
    entities.value = [];
    isStale.value = false;
  } finally {
    if (latest.isCurrent(token)) {
      isLoading.value = false;
    }
  }
}

function onTap(entity: StoryGuideEntity): void {
  if (props.currentSection === null) {
    return;
  }
  const section = props.currentSection;
  void open(
    props.bookId,
    entity.entityId,
    entity.canonicalName,
    section,
    props.currentLocation,
  ).then((loaded) => {
    // Generating the summary cached it server-side, but the list snapshot is
    // stale until the next section turn. Reflect the new summary into the row
    // (and the offline cache) now, so it doesn't keep showing "Tap to reveal".
    if (loaded !== null) {
      reveal(entity.entityId, loaded.shortSummary, section);
    }
    return loaded;
  });
}

function reveal(entityId: string, shortSummary: string, section: number): void {
  entities.value = entities.value.map((entity) =>
    entity.entityId === entityId ? { ...entity, shortSummary } : entity,
  );
  void entitiesApi.cacheListSummary(props.bookId, section, entityId, shortSummary);
}

// Load when first shown and whenever the reading position behind it changes — the
// full (section, location) boundary, plus a reprocess (cacheVersion), not section
// alone. A merge (mergeRevision) also reloads, since it rewrites the entity set behind
// an open panel. Clear the type filter on reload so a chip selected for the previous
// section can't hide an otherwise-populated list.
watch(
  () =>
    [
      props.bookId,
      props.currentSection,
      props.currentLocation,
      props.cacheVersion,
      mergeRevision.value,
    ] as const,
  () => {
    activeType.value = null;
    void load();
  },
  { immediate: true },
);
</script>

<template>
  <div class="flex h-full flex-col">
    <div class="mb-3">
      <input
        v-model="search"
        type="search"
        placeholder="Search people, places, things…"
        class="w-full rounded-lg border border-[var(--sm-muted)]/20 bg-[var(--sm-surface)] px-3 py-2 text-sm text-[var(--sm-fg)] placeholder:text-[var(--sm-muted)] focus:outline-none"
      />
    </div>

    <div v-if="presentTypes.length > 1" class="mb-3 flex flex-wrap gap-1.5">
      <button
        type="button"
        class="rounded-full border border-[var(--sm-muted)]/20 px-3 py-1 text-xs font-medium"
        :class="
          activeType === null
            ? 'bg-[var(--sm-surface)] text-[var(--sm-fg)]'
            : 'text-[var(--sm-muted)] hover:text-[var(--sm-fg)]'
        "
        @click="activeType = null"
      >
        All
      </button>
      <button
        v-for="type in presentTypes"
        :key="type"
        type="button"
        class="rounded-full border border-[var(--sm-muted)]/20 px-3 py-1 text-xs font-medium"
        :class="
          activeType === type
            ? 'bg-[var(--sm-surface)] text-[var(--sm-fg)]'
            : 'text-[var(--sm-muted)] hover:text-[var(--sm-fg)]'
        "
        @click="activeType = type"
      >
        {{ entityTypePlural(type) }}
      </button>
    </div>

    <MemoryStaleBadge v-if="isStale && !isLoading && error === null" class="mb-3" />

    <div v-if="isLoading" class="space-y-3" aria-busy="true">
      <div
        v-for="n in 4"
        :key="n"
        class="h-12 animate-pulse rounded-lg bg-[var(--sm-surface)]"
      ></div>
    </div>

    <p v-else-if="error" class="text-sm text-[var(--sm-muted)]">{{ error }}</p>

    <MemoryEmptyState
      v-else-if="entities.length === 0"
      :processing="isProcessing"
      idle="Nothing in the Story Guide yet — keep reading and people, places, and things you meet will appear here."
    />

    <p v-else-if="filtered.length === 0" class="text-sm text-[var(--sm-muted)]">
      No matches for “{{ search }}”.
    </p>

    <ul v-else class="space-y-2">
      <li v-for="entity in filtered" :key="entity.entityId">
        <button
          type="button"
          class="flex w-full flex-col gap-1 rounded-lg border border-[var(--sm-muted)]/15 bg-[var(--sm-surface)]/40 px-3 py-2 text-left hover:bg-[var(--sm-surface)]"
          @click="onTap(entity)"
        >
          <span class="flex items-baseline gap-2">
            <span class="text-sm font-semibold text-[var(--sm-fg)]">{{
              entity.canonicalName
            }}</span>
            <EntityTypeBadge :type="entity.entityType" />
          </span>
          <span
            v-if="entity.shortSummary !== null"
            class="line-clamp-2 text-xs text-[var(--sm-muted)]"
          >
            {{ entity.shortSummary }}
          </span>
          <span v-else class="text-xs italic text-[var(--sm-muted)]/70">
            Tap to reveal a spoiler-safe summary
          </span>
          <span v-if="metaLine(entity).length > 0" class="text-[11px] text-[var(--sm-muted)]/80">
            {{ metaLine(entity) }}
          </span>
        </button>
      </li>
    </ul>
  </div>
</template>

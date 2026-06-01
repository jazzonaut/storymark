<script setup lang="ts">
import { computed, ref, watch } from "vue";
import type { StoryGuideEntity } from "@storymark/shared-types";
import EntityTypeBadge from "@/components/reader/EntityTypeBadge.vue";
import { entitiesApi } from "@/api/entities-api";
import { useEntitySummary } from "@/composables/use-entity-summary";
import { ApiError } from "@/api/api-error";

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
}>();

const { open } = useEntitySummary();

const entities = ref<readonly StoryGuideEntity[]>([]);
const isLoading = ref(false);
const error = ref<string | null>(null);
const search = ref("");

const filtered = computed<readonly StoryGuideEntity[]>(() => {
  const query = search.value.trim().toLowerCase();
  if (query.length === 0) {
    return entities.value;
  }
  return entities.value.filter(
    (entity) =>
      entity.canonicalName.toLowerCase().includes(query) ||
      entity.aliases.some((alias) => alias.toLowerCase().includes(query)),
  );
});

async function load(): Promise<void> {
  if (props.currentSection === null) {
    entities.value = [];
    return;
  }
  isLoading.value = true;
  error.value = null;
  try {
    entities.value = await entitiesApi.list(
      props.bookId,
      props.currentSection,
      props.currentLocation,
    );
  } catch (caught) {
    error.value =
      caught instanceof ApiError
        ? "Couldn't load the Story Guide just now."
        : "The Story Guide is unavailable offline.";
    entities.value = [];
  } finally {
    isLoading.value = false;
  }
}

function onTap(entity: StoryGuideEntity): void {
  if (props.currentSection === null) {
    return;
  }
  void open(
    props.bookId,
    entity.entityId,
    entity.canonicalName,
    props.currentSection,
    props.currentLocation,
  );
}

// Load when first shown and whenever the reading position behind it changes.
watch(
  () => [props.bookId, props.currentSection] as const,
  () => {
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

    <div v-if="isLoading" class="space-y-3" aria-busy="true">
      <div
        v-for="n in 4"
        :key="n"
        class="h-12 animate-pulse rounded-lg bg-[var(--sm-surface)]"
      ></div>
    </div>

    <p v-else-if="error" class="text-sm text-[var(--sm-muted)]">{{ error }}</p>

    <p v-else-if="entities.length === 0" class="text-sm text-[var(--sm-muted)]">
      Nothing in the Story Guide yet — keep reading and people, places, and things you meet will
      appear here.
    </p>

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
        </button>
      </li>
    </ul>
  </div>
</template>

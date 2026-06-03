<script setup lang="ts">
import { defineAsyncComponent, watch } from "vue";
import MemoryEmptyState from "@/components/reader/MemoryEmptyState.vue";
import RefreshStatusIndicator from "@/components/reader/RefreshStatusIndicator.vue";
import { useRelationshipGraph } from "@/composables/use-relationship-graph";
import { useEntitySummary } from "@/composables/use-entity-summary";
import { useEntityMerge } from "@/composables/use-entity-merge";

// ECharts is heavy; load the graph chart only when the Graph tab is actually opened.
const RelationshipGraphChart = defineAsyncComponent(
  () => import("@/components/reader/RelationshipGraphChart.vue"),
);

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  cacheVersion: string | null;
  isProcessing: boolean;
}>();

const { nodes, edges, isLoading, isRefreshing, error, fallback, load } = useRelationshipGraph();
const { open } = useEntitySummary();
const { mergeRevision } = useEntityMerge();

// Tap a node → the same global entity popup the Story Guide and highlights open.
function onNodeTap(payload: { entityId: string; name: string }): void {
  if (props.currentSection === null) {
    return;
  }
  void open(
    props.bookId,
    payload.entityId,
    payload.name,
    props.currentSection,
    props.currentLocation,
    props.cacheVersion,
    !props.isProcessing,
  );
}

// Reload on the full spoiler boundary (section + location) and on a reprocess
// (cacheVersion), not section alone. A merge (mergeRevision) reloads too, since it
// rewrites the graph's nodes and edges.
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
    if (props.currentSection !== null) {
      void load(
        props.bookId,
        props.currentSection,
        props.currentLocation,
        props.cacheVersion,
        !props.isProcessing,
      );
    }
  },
  { immediate: true },
);
</script>

<template>
  <div class="flex h-full min-h-0 flex-col gap-3">
    <div
      v-if="isLoading"
      class="min-h-[60vh] w-full flex-1 animate-pulse rounded-lg bg-[var(--sm-surface)] sm:min-h-0"
    ></div>

    <p v-else-if="error" class="text-sm text-[var(--sm-muted)]">{{ error }}</p>

    <MemoryEmptyState
      v-else-if="nodes.length === 0"
      :processing="isProcessing"
      idle="No one to map yet — characters and how they connect appear here as you read."
    />

    <template v-else>
      <RefreshStatusIndicator :refreshing="isRefreshing" :fallback="fallback" />
      <RelationshipGraphChart :nodes="nodes" :edges="edges" @node-tap="onNodeTap" />
      <p class="text-center text-xs text-[var(--sm-muted)]">
        Drag to pan · pinch or scroll to zoom · tap a name or a connecting line for details.
      </p>
    </template>
  </div>
</template>

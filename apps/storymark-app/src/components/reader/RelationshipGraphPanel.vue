<script setup lang="ts">
import { defineAsyncComponent, watch } from "vue";
import MemoryEmptyState from "@/components/reader/MemoryEmptyState.vue";
import { useRelationshipGraph } from "@/composables/use-relationship-graph";
import { useEntitySummary } from "@/composables/use-entity-summary";

// ECharts is heavy; load the graph chart only when the Graph tab is actually opened.
const RelationshipGraphChart = defineAsyncComponent(
  () => import("@/components/reader/RelationshipGraphChart.vue"),
);

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  isProcessing: boolean;
}>();

const { nodes, edges, isLoading, error, load } = useRelationshipGraph();
const { open } = useEntitySummary();

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
  );
}

watch(
  () => [props.bookId, props.currentSection] as const,
  () => {
    if (props.currentSection !== null) {
      void load(props.bookId, props.currentSection, props.currentLocation);
    }
  },
  { immediate: true },
);
</script>

<template>
  <div class="flex h-full flex-col gap-3">
    <div v-if="isLoading" class="h-72 w-full animate-pulse rounded-lg bg-[var(--sm-surface)]"></div>

    <p v-else-if="error" class="text-sm text-[var(--sm-muted)]">{{ error }}</p>

    <MemoryEmptyState
      v-else-if="nodes.length === 0"
      :processing="isProcessing"
      idle="No one to map yet — characters and how they connect appear here as you read."
    />

    <template v-else>
      <RelationshipGraphChart :nodes="nodes" :edges="edges" @node-tap="onNodeTap" />
      <p class="text-center text-xs text-[var(--sm-muted)]">
        Drag to rearrange · pinch or scroll to zoom · tap a name to learn more.
      </p>
    </template>
  </div>
</template>

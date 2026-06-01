<script setup lang="ts">
import type { GraphSeriesOption } from "echarts/charts";
import { GraphChart } from "echarts/charts";
import type { ComposeOption, ECElementEvent, ElementEvent } from "echarts/core";
import { use } from "echarts/core";
import { CanvasRenderer } from "echarts/renderers";
import { computed, ref, watch } from "vue";
import VChart from "vue-echarts";
import type { GraphEdge, GraphNode } from "@/composables/use-relationship-graph";
import { entityTypeColor } from "@/utils/entity-type";
import { useReaderSettings } from "@/composables/use-reader-settings";
import type { ReaderTheme } from "@/db/storymark-db";

use([GraphChart, CanvasRenderer]);

type ChartOption = ComposeOption<GraphSeriesOption>;

/** The node a first tap selected: shown as a small card that a second tap opens.
 * `x`/`y` are pixel offsets within the chart; `below` flips the card under the node
 * when it sits too near the top edge to fit above it. */
interface SelectedNode {
  readonly entityId: string;
  readonly name: string;
  readonly color: string;
  readonly x: number;
  readonly y: number;
  readonly below: boolean;
}

// ECharts' canvas renderer can't resolve CSS custom properties, so the reader's
// `--sm-fg`/`--sm-muted` tokens (theme.css) are mirrored here as concrete values
// keyed by the active theme — labels stay legible on every theme.
const LABEL_COLORS: Record<ReaderTheme, { fg: string; muted: string }> = {
  light: { fg: "#1c1917", muted: "#57534e" },
  sepia: { fg: "#4b3b2a", muted: "#6f5b43" },
  dark: { fg: "#e7e5e4", muted: "#a8a29e" },
};

const props = defineProps<{
  nodes: readonly GraphNode[];
  edges: readonly GraphEdge[];
}>();

const emit = defineEmits<{
  (e: "node-tap", payload: { entityId: string; name: string }): void;
}>();

const { settings } = useReaderSettings();
const labelColors = computed(() => LABEL_COLORS[settings.theme]);

const selected = ref<SelectedNode | null>(null);

// Mention count isn't on the entity DTO, so node size reflects how connected the
// entity is (incident relationships); isolated nodes keep a legible minimum.
function symbolSize(degree: number): number {
  return Math.min(46, 14 + degree * 8);
}

function polarityColor(polarity: string | null): string {
  const value = (polarity ?? "").toLowerCase();
  if (value.includes("pos")) {
    return "#10b981";
  }
  if (value.includes("neg")) {
    return "#ef4444";
  }
  return "#94a3b8";
}

const option = computed<ChartOption>(() => {
  // Links reference nodes by id: each node is registered with `id: entityId`, and
  // ECharts keys graph nodes by `id` when present (not by name). The composable has
  // already dropped any edge whose endpoints aren't among the visible nodes.
  const links = props.edges.map((edge) => ({
    source: edge.source,
    target: edge.target,
    label: { formatter: edge.relationshipType },
    lineStyle: { color: polarityColor(edge.polarity), width: 1.5, opacity: 0.7 },
  }));
  return {
    series: [
      {
        type: "graph",
        layout: "force",
        roam: true,
        draggable: true,
        force: { repulsion: 160, edgeLength: [60, 140], gravity: 0.08 },
        emphasis: { focus: "adjacency" },
        label: { show: true, position: "right", fontSize: 11, color: labelColors.value.fg },
        edgeLabel: { show: true, fontSize: 9, color: labelColors.value.muted },
        data: props.nodes.map((node) => ({
          id: node.entityId,
          name: node.name,
          entityType: node.entityType,
          symbolSize: symbolSize(node.degree),
          itemStyle: { color: entityTypeColor(node.entityType) },
        })),
        links,
      },
    ],
  };
});

// First tap: select the node and show its card. A second tap is on the card (an HTML
// element), not the canvas — so it can't be swallowed by ECharts' touch/roam handling,
// which is why a single canvas tap never opened anything on mobile.
function onClick(params: ECElementEvent): void {
  if (params.dataType !== "node") {
    selected.value = null;
    return;
  }
  const data = params.data;
  if (typeof data !== "object" || data === null || !("id" in data) || !("name" in data)) {
    return;
  }
  const { id, name } = data;
  if (typeof id !== "string" || typeof name !== "string") {
    return;
  }
  const entityType =
    "entityType" in data && typeof data.entityType === "string" ? data.entityType : "";
  const event = params.event;
  const x = event ? event.offsetX : 0;
  const y = event ? event.offsetY : 0;
  selected.value = { entityId: id, name, color: entityTypeColor(entityType), x, y, below: y < 110 };
}

function openDetails(): void {
  const node = selected.value;
  if (node === null) {
    return;
  }
  emit("node-tap", { entityId: node.entityId, name: node.name });
  selected.value = null;
}

function clearSelection(): void {
  selected.value = null;
}

// zr:click fires for every chart click, including blank space (no target) — the one
// place an ECharts series `click` never reaches. Dismiss the card on a blank tap.
function onBlankClick(event: ElementEvent): void {
  if (!event.target) {
    selected.value = null;
  }
}

// Roaming, dragging a node, or a new section all move nodes; the card's pinned pixel
// position would no longer point at its node, so drop it.
watch(() => props.nodes, clearSelection);
</script>

<template>
  <!-- touch-action:none lets ECharts own the pinch/drag gesture so the browser
       doesn't hijack it as a viewport (page) zoom. -->
  <div class="relative min-h-[60vh] w-full flex-1 touch-none sm:min-h-0">
    <!-- The 60vh floor lives on the chart element itself, not just the wrapper. On
         mobile the Memory panel is a content-height bottom sheet, so nothing in the
         chain has a resolved height for `h-full` to read and the canvas collapses to
         0 — ECharts' roam View coordinate system then builds a null transform from the
         empty rect and throws. Dropped on sm:, where the full-height desktop rail gives
         the wrapper a real height that `h-full` fills. -->
    <VChart
      :option="option"
      autoresize
      class="h-full min-h-[60vh] w-full sm:min-h-0"
      @click="onClick"
      @zr:click="onBlankClick"
      @graphroam="clearSelection"
      @zr:dragstart="clearSelection"
    />

    <!-- pointer-events-none lets drags pass through to the canvas; only the card
         itself takes taps. -->
    <div v-if="selected" class="pointer-events-none absolute inset-0 z-10">
      <button
        type="button"
        class="pointer-events-auto absolute flex max-w-[14rem] -translate-x-1/2 items-center gap-2 rounded-xl border border-[var(--sm-muted)]/20 bg-[var(--sm-bg)] py-1.5 pr-2.5 pl-2 shadow-lg"
        :class="selected.below ? '' : '-translate-y-full'"
        :style="{ left: `${selected.x}px`, top: `${selected.y + (selected.below ? 16 : -16)}px` }"
        @click="openDetails"
      >
        <span
          class="h-2.5 w-2.5 shrink-0 rounded-full"
          :style="{ backgroundColor: selected.color }"
        ></span>
        <span class="flex min-w-0 flex-col items-start leading-tight">
          <span class="max-w-[10rem] truncate text-sm font-semibold text-[var(--sm-fg)]">
            {{ selected.name }}
          </span>
          <span class="text-xs text-[var(--sm-muted)]">View details ›</span>
        </span>
      </button>
    </div>
  </div>
</template>

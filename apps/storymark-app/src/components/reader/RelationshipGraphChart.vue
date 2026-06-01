<script setup lang="ts">
import type { GraphSeriesOption } from "echarts/charts";
import { GraphChart } from "echarts/charts";
import type { ComposeOption, ECElementEvent, ElementEvent } from "echarts/core";
import { use } from "echarts/core";
import { CanvasRenderer } from "echarts/renderers";
import { computed, ref, watch } from "vue";
import { useEventListener } from "@vueuse/core";
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
        // "move" (not true) so ECharts owns pan/drag but NOT scale: its built-in
        // pinch zoom applies a fixed step per touchmove event, compounding to a wild
        // zoom on mobile. Zoom is driven by our own gesture handlers below instead.
        roam: "move",
        scaleLimit: { min: 0.5, max: 4 },
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

const chart = ref<InstanceType<typeof VChart> | null>(null);
const container = ref<HTMLDivElement | null>(null);

// Exponent on the raw finger-distance ratio: 1 maps zoom 1:1 to the pinch span,
// below 1 makes it gentler. Because the per-frame ratios multiply, the total zoom
// across a gesture is (finalSpan / startSpan) ** PINCH_SENSITIVITY regardless of how
// many touchmove events fire — which is exactly what ECharts' fixed-step roam got wrong.
const PINCH_SENSITIVITY = 0.75;
const WHEEL_ZOOM_STEP = 1.1;

let pinchSpan: number | null = null;

// graphRoam applies `zoom` as a delta multiplier about the origin (chart pixel space)
// and clamps to scaleLimit. The resulting `graphroam` event clears the selection card.
function zoomBy(zoom: number, clientX: number, clientY: number): void {
  const el = container.value;
  if (el === null) {
    return;
  }
  const rect = el.getBoundingClientRect();
  chart.value?.dispatchAction({
    type: "graphRoam",
    seriesIndex: 0,
    zoom,
    originX: clientX - rect.left,
    originY: clientY - rect.top,
  });
}

// Midpoint and separation of the first two touches; null unless two are down.
function twoFingerSpan(
  touches: TouchList,
): { distance: number; clientX: number; clientY: number } | null {
  const a = touches[0];
  const b = touches[1];
  if (a === undefined || b === undefined) {
    return null;
  }
  return {
    distance: Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY),
    clientX: (a.clientX + b.clientX) / 2,
    clientY: (a.clientY + b.clientY) / 2,
  };
}

function onWheel(event: WheelEvent): void {
  event.preventDefault();
  zoomBy(event.deltaY < 0 ? WHEEL_ZOOM_STEP : 1 / WHEEL_ZOOM_STEP, event.clientX, event.clientY);
}

function onTouchStart(event: TouchEvent): void {
  pinchSpan = twoFingerSpan(event.touches)?.distance ?? null;
}

function onTouchMove(event: TouchEvent): void {
  const span = twoFingerSpan(event.touches);
  const previous = pinchSpan;
  if (span === null) {
    return;
  }
  pinchSpan = span.distance;
  if (previous === null || previous <= 0 || span.distance <= 0) {
    return;
  }
  event.preventDefault();
  zoomBy(Math.pow(span.distance / previous, PINCH_SENSITIVITY), span.clientX, span.clientY);
}

function onTouchEnd(event: TouchEvent): void {
  if (event.touches.length < 2) {
    pinchSpan = null;
  }
}

useEventListener(container, "wheel", onWheel, { passive: false });
useEventListener(container, "touchstart", onTouchStart, { passive: true });
useEventListener(container, "touchmove", onTouchMove, { passive: false });
useEventListener(container, "touchend", onTouchEnd);
useEventListener(container, "touchcancel", onTouchEnd);

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
  <!-- touch-action:none stops the browser hijacking the pinch/drag as a viewport
       (page) zoom, so ECharts can pan and our handlers can drive the zoom. -->
  <div ref="container" class="relative min-h-[60vh] w-full flex-1 touch-none sm:min-h-0">
    <!-- The 60vh floor lives on the chart element itself, not just the wrapper. On
         mobile the Memory panel is a content-height bottom sheet, so nothing in the
         chain has a resolved height for `h-full` to read and the canvas collapses to
         0 — ECharts' roam View coordinate system then builds a null transform from the
         empty rect and throws. Dropped on sm:, where the full-height desktop rail gives
         the wrapper a real height that `h-full` fills. -->
    <VChart
      ref="chart"
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

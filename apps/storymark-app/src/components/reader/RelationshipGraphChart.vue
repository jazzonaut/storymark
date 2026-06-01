<script setup lang="ts">
import type { GraphSeriesOption } from "echarts/charts";
import { GraphChart } from "echarts/charts";
import type { TooltipComponentOption } from "echarts/components";
import { TooltipComponent } from "echarts/components";
import type { ComposeOption, ECElementEvent } from "echarts/core";
import { use } from "echarts/core";
import { CanvasRenderer } from "echarts/renderers";
import { computed } from "vue";
import VChart from "vue-echarts";
import type { GraphEdge, GraphNode } from "@/composables/use-relationship-graph";
import { entityTypeColor } from "@/utils/entity-type";
import { useReaderSettings } from "@/composables/use-reader-settings";
import type { ReaderTheme } from "@/db/storymark-db";

use([GraphChart, TooltipComponent, CanvasRenderer]);

type ChartOption = ComposeOption<GraphSeriesOption | TooltipComponentOption>;

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
    tooltip: { trigger: "item", confine: true },
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
          symbolSize: symbolSize(node.degree),
          itemStyle: { color: entityTypeColor(node.entityType) },
        })),
        links,
      },
    ],
  };
});

function onClick(params: ECElementEvent): void {
  if (params.dataType !== "node") {
    return;
  }
  const data = params.data;
  if (typeof data !== "object" || data === null || !("id" in data) || !("name" in data)) {
    return;
  }
  const { id, name } = data;
  if (typeof id === "string" && typeof name === "string") {
    emit("node-tap", { entityId: id, name });
  }
}
</script>

<template>
  <div class="h-72 w-full">
    <VChart :option="option" autoresize class="h-full w-full" @click="onClick" />
  </div>
</template>

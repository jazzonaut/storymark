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

// Edges reference entities by id; ECharts links resolve against node names, so
// translate through this map (every endpoint is a known node — the composable
// dropped dangling edges).
const nameById = computed<ReadonlyMap<string, string>>(
  () => new Map(props.nodes.map((node) => [node.entityId, node.name])),
);

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
  const links = props.edges.flatMap((edge) => {
    const source = nameById.value.get(edge.source);
    const target = nameById.value.get(edge.target);
    if (source === undefined || target === undefined) {
      return [];
    }
    return [
      {
        source,
        target,
        label: { formatter: edge.relationshipType },
        lineStyle: { color: polarityColor(edge.polarity), width: 1.5, opacity: 0.7 },
      },
    ];
  });
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
  <VChart :option="option" autoresize class="h-72 w-full" @click="onClick" />
</template>

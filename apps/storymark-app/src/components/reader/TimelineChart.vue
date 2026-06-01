<script setup lang="ts">
import type { ScatterSeriesOption } from "echarts/charts";
import { ScatterChart } from "echarts/charts";
import type {
  GridComponentOption,
  TooltipComponentOption,
  DataZoomComponentOption,
} from "echarts/components";
import { DataZoomComponent, GridComponent, TooltipComponent } from "echarts/components";
import type { ComposeOption } from "echarts/core";
import { use } from "echarts/core";
import { CanvasRenderer } from "echarts/renderers";
import { computed } from "vue";
import VChart from "vue-echarts";
import type { BookEvent } from "@storymark/shared-types";
import { eventTypeColor, eventTypeLabel } from "@/utils/event-type";

use([ScatterChart, GridComponent, TooltipComponent, DataZoomComponent, CanvasRenderer]);

type ChartOption = ComposeOption<
  ScatterSeriesOption | GridComponentOption | TooltipComponentOption | DataZoomComponentOption
>;

const props = defineProps<{
  events: readonly BookEvent[];
  entityNames: ReadonlyMap<string, string>;
}>();

const emit = defineEmits<{
  (e: "select", event: BookEvent): void;
}>();

// Chronological order = left-to-right on the timeline.
const ordered = computed<readonly BookEvent[]>(() =>
  props.events.toSorted((a, b) =>
    a.sectionIndex === b.sectionIndex
      ? a.locationStart - b.locationStart
      : a.sectionIndex - b.sectionIndex,
  ),
);

function involvedNames(event: BookEvent): string {
  const names = event.involvedEntityIds
    .map((id) => props.entityNames.get(id))
    .filter((name): name is string => name !== undefined);
  return names.join(", ");
}

// Tooltip content is precomputed into each item's `name` (rendered as HTML by the
// "{b}" template) — avoids a typed formatter callback against ECharts' option types.
function tooltipHtml(event: BookEvent): string {
  const who = involvedNames(event);
  const lines = [
    `<strong>${eventTypeLabel(event.eventType)}</strong> · Section ${String(event.sectionIndex)}`,
    event.description,
  ];
  if (who.length > 0) {
    lines.push(`<em>${who}</em>`);
  }
  return lines.join("<br/>");
}

const option = computed<ChartOption>(() => {
  const items = ordered.value;
  return {
    grid: { left: 12, right: 12, top: 16, bottom: 28, containLabel: false },
    tooltip: { trigger: "item", confine: true, formatter: "{b}" },
    xAxis: { type: "value", min: -0.5, max: Math.max(0.5, items.length - 0.5), show: false },
    yAxis: { type: "value", min: -1, max: 1, show: false },
    dataZoom:
      items.length > 8
        ? [
            { type: "inside", filterMode: "none" },
            { type: "slider", height: 14, bottom: 4 },
          ]
        : [],
    series: [
      {
        type: "scatter",
        symbolSize: 18,
        data: items.map((event, index) => ({
          name: tooltipHtml(event),
          value: [index, 0],
          itemStyle: { color: eventTypeColor(event.eventType) },
        })),
      },
    ],
  };
});

function onClick(params: { dataIndex?: number }): void {
  if (params.dataIndex === undefined) {
    return;
  }
  const event = ordered.value[params.dataIndex];
  if (event !== undefined) {
    emit("select", event);
  }
}
</script>

<template>
  <VChart :option="option" autoresize class="h-56 w-full" @click="onClick" />
</template>

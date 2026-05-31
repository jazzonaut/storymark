<script setup lang="ts">
import type { BarSeriesOption } from "echarts/charts";
import { BarChart } from "echarts/charts";
import type { GridComponentOption, TooltipComponentOption } from "echarts/components";
import { GridComponent, TooltipComponent } from "echarts/components";
import type { ComposeOption } from "echarts/core";
import { use } from "echarts/core";
import { CanvasRenderer } from "echarts/renderers";
import { computed } from "vue";
import VChart from "vue-echarts";

use([BarChart, GridComponent, TooltipComponent, CanvasRenderer]);

type ChartOption = ComposeOption<BarSeriesOption | GridComponentOption | TooltipComponentOption>;

const props = defineProps<{
  categories: readonly string[];
  values: readonly number[];
  seriesName: string;
}>();

const option = computed<ChartOption>(() => ({
  tooltip: { trigger: "axis" },
  grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
  xAxis: { type: "category", data: [...props.categories] },
  yAxis: { type: "value" },
  series: [{ name: props.seriesName, type: "bar", data: [...props.values] }],
}));
</script>

<template>
  <VChart :option="option" autoresize class="h-80 w-full" />
</template>

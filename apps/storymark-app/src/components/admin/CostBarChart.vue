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
import { useReaderSettings } from "@/composables/use-reader-settings";

use([BarChart, GridComponent, TooltipComponent, CanvasRenderer]);

type ChartOption = ComposeOption<BarSeriesOption | GridComponentOption | TooltipComponentOption>;

const props = defineProps<{
  categories: readonly string[];
  values: readonly number[];
  seriesName: string;
}>();

const { settings } = useReaderSettings();

// ECharts has no notion of the app theme; mirror `data-theme` (loaded app-wide
// in App.vue) onto its built-in dark theme so axis labels stay readable.
// "default" is not a registered theme — an unknown name is exactly how ECharts
// selects its default styling, and it keeps the prop a plain string under
// exactOptionalPropertyTypes.
const chartTheme = computed<string>(() => (settings.theme === "dark" ? "dark" : "default"));

const option = computed<ChartOption>(() => ({
  // The built-in dark theme paints its own near-black canvas; the page
  // background already provides one.
  backgroundColor: "transparent",
  tooltip: { trigger: "axis" },
  grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
  xAxis: { type: "category", data: [...props.categories] },
  yAxis: { type: "value" },
  series: [{ name: props.seriesName, type: "bar", data: [...props.values] }],
}));
</script>

<template>
  <VChart :option="option" :theme="chartTheme" autoresize class="h-80 w-full" />
</template>

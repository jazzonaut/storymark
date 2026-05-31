<script setup lang="ts">
import Button from "primevue/button";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import SelectButton from "primevue/selectbutton";
import { computed, defineAsyncComponent, onMounted, ref } from "vue";
import type { TaskCostItem } from "@/api/admin-types";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";
import { formatCost, formatTokens } from "@/utils/llm-format";

// ECharts wrapper is loaded lazily, never on the reader's hot path (02-vue.md).
const CostBarChart = defineAsyncComponent(() => import("@/components/admin/CostBarChart.vue"));

type Metric = "cost" | "calls";

const { costSummary, isLoading, loadCostSummary } = useAdmin();
const { report } = useAdminError();

const metric = ref<Metric>("cost");
const metricOptions: readonly { label: string; value: Metric }[] = [
  { label: "Cost (USD)", value: "cost" },
  { label: "Calls", value: "calls" },
];

const tasks = computed<readonly TaskCostItem[]>(() => costSummary.value?.tasks ?? []);
const categories = computed<readonly string[]>(() => tasks.value.map((t) => t.task));
const values = computed<readonly number[]>(() =>
  metric.value === "cost"
    ? tasks.value.map((t) => t.costEstimate ?? 0)
    : tasks.value.map((t) => t.callCount),
);
const seriesName = computed<string>(() => (metric.value === "cost" ? "Cost (USD)" : "Calls"));

const totalTokens = computed<number>(() => {
  const summary = costSummary.value;
  if (summary === null) {
    return 0;
  }
  return (summary.tokensIn ?? 0) + (summary.tokensOut ?? 0);
});

async function refresh(): Promise<void> {
  try {
    await loadCostSummary();
  } catch (error) {
    report(error);
  }
}

onMounted(refresh);
</script>

<template>
  <section>
    <div class="mb-4 flex items-center justify-between">
      <h2 class="text-lg font-semibold">Cost Meter</h2>
      <Button label="Refresh" size="small" outlined :loading="isLoading" @click="refresh" />
    </div>

    <template v-if="costSummary !== null">
      <div class="mb-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
        <div class="rounded border border-zinc-200 p-4 dark:border-zinc-800">
          <div class="text-xs opacity-60">Window</div>
          <div class="text-lg font-semibold">Last {{ costSummary.windowDays }} days</div>
        </div>
        <div class="rounded border border-zinc-200 p-4 dark:border-zinc-800">
          <div class="text-xs opacity-60">Calls</div>
          <div class="text-lg font-semibold">{{ costSummary.callCount }}</div>
        </div>
        <div class="rounded border border-zinc-200 p-4 dark:border-zinc-800">
          <div class="text-xs opacity-60">Tokens (in + out)</div>
          <div class="text-lg font-semibold">{{ totalTokens }}</div>
        </div>
        <div class="rounded border border-zinc-200 p-4 dark:border-zinc-800">
          <div class="text-xs opacity-60">Total cost</div>
          <div class="text-lg font-semibold">
            <template v-if="costSummary.costEstimate !== null">
              {{ formatCost(costSummary.costEstimate) }}
            </template>
            <span v-else class="text-sm font-normal opacity-70">No cost rates configured</span>
          </div>
        </div>
      </div>

      <div class="mb-4">
        <SelectButton
          v-model="metric"
          :options="[...metricOptions]"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          size="small"
        />
      </div>

      <CostBarChart
        v-if="tasks.length > 0"
        :categories="categories"
        :values="values"
        :series-name="seriesName"
      />
      <p v-else class="opacity-70">No LLM calls in the last {{ costSummary.windowDays }} days.</p>

      <DataTable
        v-if="tasks.length > 0"
        :value="[...tasks]"
        :loading="isLoading"
        data-key="task"
        size="small"
        class="mt-6"
      >
        <Column field="task" header="Task" />
        <Column field="callCount" header="Calls" />
        <Column header="Tokens (in / out)">
          <template #body="{ data }">
            {{ formatTokens((data as TaskCostItem).tokensIn, (data as TaskCostItem).tokensOut) }}
          </template>
        </Column>
        <Column header="Cost">
          <template #body="{ data }">{{
            formatCost((data as TaskCostItem).costEstimate)
          }}</template>
        </Column>
      </DataTable>
    </template>

    <p v-else class="opacity-70">{{ isLoading ? "Loading…" : "No data. Try refreshing." }}</p>
  </section>
</template>

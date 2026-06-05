<script setup lang="ts">
import Button from "primevue/button";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import Select from "primevue/select";
import Tag from "primevue/tag";
import ToggleButton from "primevue/togglebutton";
import { computed, onMounted, ref, watch } from "vue";
import type { LlmCallItem } from "@/api/admin-types";
import AdminPageHeader from "@/components/admin/AdminPageHeader.vue";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";
import { formatDateTime } from "@/utils/format-date-time";
import { formatCost, formatTokens } from "@/utils/llm-format";

const ALL_TASKS = "all";

const { llmCalls, llmCallsWindow, llmCallsLoading, loadLlmCalls } = useAdmin();
const { report } = useAdminError();

const taskFilter = ref<string>(ALL_TASKS);
const futureOnly = ref<boolean>(false);
const errorsOnly = ref<boolean>(false);
const expandedRows = ref<Record<string, boolean>>({});

const distinctTasks = computed<readonly string[]>(() =>
  [...llmCalls.value]
    .filter((call, index, all) => all.findIndex((c) => c.task === call.task) === index)
    .map((call) => call.task)
    .sort((a, b) => a.localeCompare(b)),
);

const taskOptions = computed<readonly { label: string; value: string }[]>(() => [
  { label: "All tasks", value: ALL_TASKS },
  ...distinctTasks.value.map((task) => ({ label: task, value: task })),
]);

const filtered = computed<readonly LlmCallItem[]>(() =>
  llmCalls.value.filter(
    (call) =>
      (taskFilter.value === ALL_TASKS || call.task === taskFilter.value) &&
      (!futureOnly.value || call.usedFutureContent) &&
      (!errorsOnly.value || !call.succeeded),
  ),
);

const atWindow = computed<boolean>(() => llmCalls.value.length >= llmCallsWindow);

// A task selected before a refresh may fall out of the loaded window; drop the
// stale filter rather than silently showing an empty table.
watch(distinctTasks, (tasks) => {
  if (taskFilter.value !== ALL_TASKS && !tasks.includes(taskFilter.value)) {
    taskFilter.value = ALL_TASKS;
  }
});

function formatModel(call: LlmCallItem): string {
  return `${call.provider}:${call.model}`;
}

function formatLatency(call: LlmCallItem): string {
  return call.latencyMs === null ? "—" : `${call.latencyMs} ms`;
}

function rowClass(call: LlmCallItem): string {
  return call.succeeded ? "" : "bg-red-50 dark:bg-red-950/40";
}

async function refresh(): Promise<void> {
  try {
    await loadLlmCalls();
  } catch (error) {
    report(error);
  }
}

onMounted(refresh);
</script>

<template>
  <section>
    <AdminPageHeader title="LLM Calls">
      <template #actions>
        <Button label="Refresh" size="small" outlined :loading="llmCallsLoading" @click="refresh" />
      </template>
    </AdminPageHeader>

    <div class="mb-4 flex flex-wrap items-center gap-3">
      <Select
        v-model="taskFilter"
        :options="[...taskOptions]"
        option-label="label"
        option-value="value"
        size="small"
        aria-label="Filter by task"
        class="w-full sm:w-52"
      />
      <ToggleButton
        v-model="futureOnly"
        on-label="Future content only"
        off-label="Future content only"
        size="small"
      />
      <ToggleButton
        v-model="errorsOnly"
        on-label="Errors only"
        off-label="Errors only"
        size="small"
      />
    </div>

    <div class="overflow-x-auto">
      <DataTable
        v-model:expanded-rows="expandedRows"
        :value="[...filtered]"
        :loading="llmCallsLoading"
        :row-class="rowClass"
        data-key="id"
        paginator
        :rows="20"
        :rows-per-page-options="[20, 50, 100]"
        size="small"
      >
        <template #empty>
          <span class="text-muted-color">No LLM calls recorded yet.</span>
        </template>

        <Column expander style="width: 3rem" />

        <Column header="Time">
          <template #body="{ data }">
            <span class="whitespace-nowrap">{{
              formatDateTime((data as LlmCallItem).createdAt)
            }}</span>
          </template>
        </Column>

        <Column field="task" header="Task">
          <template #body="{ data }">
            <Tag :value="(data as LlmCallItem).task" severity="secondary" />
            <Tag
              v-if="!(data as LlmCallItem).succeeded"
              value="failed"
              severity="danger"
              class="ml-1"
            />
            <Tag
              v-if="(data as LlmCallItem).usedFutureContent"
              value="future"
              severity="danger"
              class="ml-1"
            />
          </template>
        </Column>

        <Column header="Model">
          <template #body="{ data }">
            <span class="font-mono text-xs">{{ formatModel(data as LlmCallItem) }}</span>
          </template>
        </Column>

        <Column header="Latency">
          <template #body="{ data }">{{ formatLatency(data as LlmCallItem) }}</template>
        </Column>

        <Column header="Tokens (in / out)">
          <template #body="{ data }">
            {{ formatTokens((data as LlmCallItem).tokensIn, (data as LlmCallItem).tokensOut) }}
          </template>
        </Column>

        <Column header="Cost">
          <template #body="{ data }">{{ formatCost((data as LlmCallItem).costEstimate) }}</template>
        </Column>

        <template #expansion="{ data }">
          <div class="space-y-3 p-3">
            <div v-if="(data as LlmCallItem).error">
              <div class="mb-1 text-xs font-medium text-red-700 dark:text-red-300">Error</div>
              <pre
                class="max-h-64 overflow-auto rounded bg-red-50 p-3 font-mono text-xs whitespace-pre-wrap text-red-800 dark:bg-red-950/40 dark:text-red-200"
                >{{ (data as LlmCallItem).error }}</pre
              >
            </div>
            <div>
              <div class="mb-1 text-xs font-medium text-muted-color">Prompt</div>
              <pre
                class="max-h-64 overflow-auto rounded bg-surface-100 p-3 font-mono text-xs whitespace-pre-wrap dark:bg-surface-800"
                >{{ (data as LlmCallItem).prompt }}</pre
              >
            </div>
            <div>
              <div class="mb-1 text-xs font-medium text-muted-color">Response</div>
              <pre
                class="max-h-64 overflow-auto rounded bg-surface-100 p-3 font-mono text-xs whitespace-pre-wrap dark:bg-surface-800"
                >{{ (data as LlmCallItem).response ?? "—" }}</pre
              >
            </div>
          </div>
        </template>
      </DataTable>
    </div>

    <p v-if="atWindow" class="mt-3 text-xs text-muted-color">
      Showing the latest {{ llmCallsWindow }} calls. Older calls are not loaded.
    </p>
  </section>
</template>

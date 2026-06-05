<script setup lang="ts">
import Button from "primevue/button";
import Checkbox from "primevue/checkbox";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import InputText from "primevue/inputtext";
import Password from "primevue/password";
import Tag from "primevue/tag";
import Textarea from "primevue/textarea";
import { useToast } from "primevue/usetoast";
import { computed, onMounted, ref, watch } from "vue";
import type { PatchSettingBody, SettingItem } from "@/api/admin-types";
import AdminPageHeader from "@/components/admin/AdminPageHeader.vue";
import EmbeddingSwapDialog from "@/components/admin/EmbeddingSwapDialog.vue";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";

interface SettingRow extends SettingItem {
  readonly namespace: string;
}

// Editing this key triggers a full reindex, so it's intercepted with a confirm
// dialog instead of the normal inline save.
const EMBEDDING_MODEL_KEY = "llm.embedding.modelId";

const { settings, settingsLoading, loadSettings, saveSetting, backfillSectionTitles } = useAdmin();
const { report } = useAdminError();
const toast = useToast();

const backfilling = ref(false);

async function runBackfillTitles(): Promise<void> {
  backfilling.value = true;
  try {
    const result = await backfillSectionTitles();
    toast.add({
      severity: "success",
      summary: "Backfill started",
      detail:
        result.sectionsToBackfill === 0
          ? "No untitled sections — nothing to do."
          : `${result.sectionsToBackfill} untitled section(s) queued; titles are filled from each section's first heading.`,
      life: 4000,
    });
  } catch (error) {
    report(error);
  } finally {
    backfilling.value = false;
  }
}

const editingKey = ref<string | null>(null);
const draftText = ref("");
const draftBool = ref(false);
const swapModelId = ref<string | null>(null);

// Any settings reload invalidates an in-flight inline edit: the row under the
// editor may have changed (a save here, or another admin), so saving the stale
// draft would clobber it. Drop the edit instead.
watch(settings, () => {
  editingKey.value = null;
});

const rows = computed<readonly SettingRow[]>(() =>
  [...settings.value]
    .map((setting) => ({ ...setting, namespace: setting.key.split(".")[0] ?? setting.key }))
    .sort((a, b) => a.key.localeCompare(b.key)),
);

function parsed(value: string): unknown {
  try {
    return JSON.parse(value);
  } catch {
    return undefined;
  }
}

function displayValue(row: SettingItem): string {
  if (row.isSecret) {
    return "••••";
  }
  if (row.valueType === "String") {
    const result = parsed(row.value);
    return typeof result === "string" ? result : row.value;
  }
  return row.value;
}

// Some settings hold whole prompt templates; rendering thousands of characters
// per row makes the table unusably tall (worst on phones). Read view clamps;
// the editor always works on the full value.
const PREVIEW_MAX_CHARS = 240;

function previewValue(row: SettingItem): string {
  const value = displayValue(row);
  return value.length > PREVIEW_MAX_CHARS ? `${value.slice(0, PREVIEW_MAX_CHARS)}…` : value;
}

function startEdit(row: SettingItem): void {
  editingKey.value = row.key;
  draftText.value = "";
  draftBool.value = false;

  if (row.isSecret) {
    return; // write-only: leave blank, the operator types a new value
  }
  if (row.valueType === "Bool") {
    draftBool.value = parsed(row.value) === true;
  } else if (row.valueType === "String") {
    const result = parsed(row.value);
    draftText.value = typeof result === "string" ? result : row.value;
  } else {
    draftText.value = row.value;
  }
}

function cancelEdit(): void {
  editingKey.value = null;
}

function resolveValue(row: SettingItem): PatchSettingBody["value"] | null {
  if (row.valueType === "Bool") {
    return draftBool.value;
  }
  if (row.valueType === "Int") {
    const numeric = draftText.value.trim() === "" ? Number.NaN : Number(draftText.value);
    if (!Number.isInteger(numeric)) {
      toast.add({
        severity: "error",
        summary: "Invalid value",
        detail: "Enter a whole number.",
        life: 4000,
      });
      return null;
    }
    return numeric;
  }
  if (row.valueType === "Json") {
    const result = parsed(draftText.value);
    if (typeof result !== "object" || result === null) {
      toast.add({
        severity: "error",
        summary: "Invalid value",
        detail: "Enter a JSON object or array.",
        life: 4000,
      });
      return null;
    }
    return result;
  }
  return draftText.value;
}

async function save(row: SettingItem): Promise<void> {
  const value = resolveValue(row);
  if (value === null) {
    return;
  }

  if (row.key === EMBEDDING_MODEL_KEY) {
    const next = String(value).trim();
    if (next === displayValue(row)) {
      toast.add({
        severity: "info",
        summary: "No change",
        detail: "The embedding model is unchanged.",
        life: 2500,
      });
      editingKey.value = null;
      return;
    }
    swapModelId.value = next;
    return;
  }

  try {
    await saveSetting(row.key, value, row.valueType);
    toast.add({ severity: "success", summary: "Saved", detail: row.key, life: 2500 });
  } catch (error) {
    report(error);
  }
}

onMounted(() => {
  void loadSettings().catch(report);
});
</script>

<template>
  <section>
    <AdminPageHeader
      title="Settings"
      description="Raw key-value configuration. Changes are live on the next read — no restart."
    />

    <div class="overflow-x-auto">
      <DataTable
        :value="rows"
        :loading="settingsLoading"
        row-group-mode="subheader"
        group-rows-by="namespace"
        sort-field="namespace"
        :sort-order="1"
        data-key="key"
        size="small"
      >
        <template #groupheader="{ data }">
          <span class="font-medium text-muted-color">{{ (data as SettingRow).namespace }}</span>
        </template>

        <Column field="key" header="Key" />

        <Column header="Type">
          <template #body="{ data }">
            <Tag :value="(data as SettingRow).valueType" severity="secondary" />
            <Tag v-if="(data as SettingRow).isSecret" value="secret" severity="warn" class="ml-1" />
          </template>
        </Column>

        <Column header="Value">
          <template #body="{ data }">
            <template v-if="editingKey === (data as SettingRow).key">
              <Checkbox
                v-if="(data as SettingRow).valueType === 'Bool'"
                v-model="draftBool"
                binary
              />
              <Password
                v-else-if="(data as SettingRow).isSecret"
                v-model="draftText"
                :feedback="false"
                toggle-mask
                fluid
                placeholder="New secret value"
              />
              <Textarea
                v-else-if="(data as SettingRow).valueType === 'Json'"
                v-model="draftText"
                class="w-full font-mono"
                rows="3"
              />
              <InputText v-else v-model="draftText" fluid />
            </template>
            <span v-else class="line-clamp-3 break-all">{{
              previewValue(data as SettingRow)
            }}</span>
          </template>
        </Column>

        <Column header="" style="width: 8rem">
          <template #body="{ data }">
            <template v-if="editingKey === (data as SettingRow).key">
              <Button
                label="Save"
                size="small"
                :disabled="(data as SettingRow).isSecret && draftText.length === 0"
                @click="save(data as SettingRow)"
              />
              <Button label="Cancel" size="small" text @click="cancelEdit" />
            </template>
            <Button v-else label="Edit" size="small" text @click="startEdit(data as SettingRow)" />
          </template>
        </Column>
      </DataTable>
    </div>

    <h3 class="mt-8 mb-2 font-medium">Maintenance</h3>
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
      <Button
        label="Backfill section titles"
        size="small"
        class="self-start"
        :loading="backfilling"
        @click="runBackfillTitles"
      />
      <span class="text-sm text-muted-color">
        Names untitled sections from their first heading. Display-only — no re-parse, LLM, or
        re-embedding.
        <a href="/hangfire" target="_blank" rel="noopener" class="underline">Track in jobs</a>.
      </span>
    </div>

    <EmbeddingSwapDialog
      :model-id="swapModelId"
      :model-label="null"
      @applied="swapModelId = null"
      @close="swapModelId = null"
    />
  </section>
</template>

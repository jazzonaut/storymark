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
import { computed, onMounted, ref } from "vue";
import type { PatchSettingBody, SettingItem } from "@/api/admin-types";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";

interface SettingRow extends SettingItem {
  readonly namespace: string;
}

const { settings, isLoading, loadSettings, saveSetting } = useAdmin();
const { report } = useAdminError();
const toast = useToast();

const editingKey = ref<string | null>(null);
const draftText = ref("");
const draftBool = ref(false);

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

  try {
    await saveSetting(row.key, value, row.valueType);
    editingKey.value = null;
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
    <h2 class="mb-4 text-lg font-semibold">Settings</h2>

    <DataTable
      :value="rows"
      :loading="isLoading"
      row-group-mode="subheader"
      group-rows-by="namespace"
      sort-field="namespace"
      :sort-order="1"
      data-key="key"
      size="small"
    >
      <template #groupheader="{ data }">
        <span class="font-medium opacity-70">{{ (data as SettingRow).namespace }}</span>
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
            <Checkbox v-if="(data as SettingRow).valueType === 'Bool'" v-model="draftBool" binary />
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
          <span v-else class="break-all">{{ displayValue(data as SettingRow) }}</span>
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
  </section>
</template>

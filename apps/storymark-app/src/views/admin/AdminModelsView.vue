<script setup lang="ts">
import Button from "primevue/button";
import Checkbox from "primevue/checkbox";
import Dialog from "primevue/dialog";
import InputText from "primevue/inputtext";
import Select from "primevue/select";
import { useToast } from "primevue/usetoast";
import { computed, onMounted, ref, watch } from "vue";
import type {
  ModelItem,
  ModelUpsertBody,
  ProviderItem,
  ProviderUpsertBody,
} from "@/api/admin-types";
import AdminPageHeader from "@/components/admin/AdminPageHeader.vue";
import EmbeddingSwapDialog from "@/components/admin/EmbeddingSwapDialog.vue";
import ModelDialog from "@/components/admin/ModelDialog.vue";
import ProviderCard from "@/components/admin/ProviderCard.vue";
import ProviderDialog from "@/components/admin/ProviderDialog.vue";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";

const TASK_PREFIX = "llm.chat.taskMap.";
const EMBEDDING_KEY = "llm.embedding.modelId";

const TASK_LABELS: Record<string, string> = {
  qa: "Q&A",
  extract: "Extraction",
  entity_summary: "Entity summary",
  selection_hint: "Selection hint",
  memory: "Memory",
  current_scene: "Current scene",
};

interface ChatTaskRow {
  key: string;
  label: string;
  modelId: string;
}

const {
  settings,
  providers,
  models,
  loadSettings,
  saveSetting,
  loadProviders,
  saveProvider,
  deleteProvider,
  loadModels,
  saveModel,
  deleteModel,
} = useAdmin();
const { report, reportWithValidation } = useAdminError();
const toast = useToast();

const initialLoading = ref(true);

const chatTasks = ref<ChatTaskRow[]>([]);
const embeddingModelId = ref("");
const embeddingSaved = ref("");
const draftTrimLocal = ref(true);
const draftMaxChunks = ref("12");
const draftMaxChunksLocal = ref("6");
const savingTrim = ref(false);

const providerTarget = ref<ProviderItem | "new" | null>(null);
const providerSaving = ref(false);
const modelTarget = ref<ModelItem | "new" | null>(null);
const modelDefaultProviderId = ref<string | null>(null);
const modelSaving = ref(false);
const deleteProviderTarget = ref<ProviderItem | null>(null);
const deleteModelTarget = ref<ModelItem | null>(null);
const deleting = ref(false);
const swapModelId = ref<string | null>(null);

const sortedProviders = computed<readonly ProviderItem[]>(() =>
  providers.value.toSorted((a, b) => a.name.localeCompare(b.name)),
);

const modelsByProvider = computed<ReadonlyMap<string, readonly ModelItem[]>>(() => {
  const map = new Map<string, ModelItem[]>();
  for (const model of models.value) {
    const list = map.get(model.providerId);
    if (list === undefined) {
      map.set(model.providerId, [model]);
    } else {
      list.push(model);
    }
  }
  for (const list of map.values()) {
    list.sort((a, b) => a.label.localeCompare(b.label));
  }
  return map;
});

const modelOptions = computed(() => models.value.map((m) => ({ label: m.label, value: m.id })));

/** Task labels (plus "Embedding") per model id — drives the in-use badges and
 * the delete-model guard. */
const assignments = computed<ReadonlyMap<string, readonly string[]>>(() => {
  const map = new Map<string, string[]>();
  const add = (modelId: string, label: string): void => {
    if (modelId.length === 0) {
      return;
    }
    const list = map.get(modelId);
    if (list === undefined) {
      map.set(modelId, [label]);
    } else {
      list.push(label);
    }
  };
  for (const row of chatTasks.value) {
    add(row.modelId, row.label);
  }
  add(embeddingSaved.value, "Embedding");
  return map;
});

const deleteProviderModelCount = computed<number>(() =>
  deleteProviderTarget.value === null
    ? 0
    : (modelsByProvider.value.get(deleteProviderTarget.value.id) ?? []).length,
);
const deleteModelTasks = computed<readonly string[]>(() =>
  deleteModelTarget.value === null
    ? []
    : (assignments.value.get(deleteModelTarget.value.id) ?? []),
);

const swapModelLabel = computed<string | null>(
  () => models.value.find((m) => m.id === swapModelId.value)?.label ?? null,
);

function parsedString(value: string): string {
  try {
    const result: unknown = JSON.parse(value);
    return typeof result === "string" ? result : value;
  } catch {
    return value;
  }
}

function parsedBool(value: string): boolean {
  try {
    return JSON.parse(value) === true;
  } catch {
    return false;
  }
}

function settingValue(key: string): string | null {
  return settings.value.find((s) => s.key === key)?.value ?? null;
}

watch(
  settings,
  () => {
    chatTasks.value = settings.value
      .filter((s) => s.key.startsWith(TASK_PREFIX))
      .map((s) => {
        const task = s.key.slice(TASK_PREFIX.length);
        return { key: s.key, label: TASK_LABELS[task] ?? task, modelId: parsedString(s.value) };
      })
      .toSorted((a, b) => a.label.localeCompare(b.label));

    const emb = settingValue(EMBEDDING_KEY);
    embeddingSaved.value = emb === null ? "" : parsedString(emb);
    embeddingModelId.value = embeddingSaved.value;

    const trim = settingValue("assistant.evidence.trimLocal");
    draftTrimLocal.value = trim === null ? true : parsedBool(trim);
    const maxChunks = settingValue("assistant.evidence.maxChunks");
    draftMaxChunks.value = maxChunks === null ? "12" : parsedString(maxChunks);
    const maxLocal = settingValue("assistant.evidence.maxChunksLocal");
    draftMaxChunksLocal.value = maxLocal === null ? "6" : parsedString(maxLocal);
  },
  { immediate: true },
);

onMounted(() => {
  void Promise.all([loadProviders(), loadModels(), loadSettings()])
    .catch(report)
    .finally(() => {
      initialLoading.value = false;
    });
});

function openAddModel(provider: ProviderItem): void {
  modelDefaultProviderId.value = provider.id;
  modelTarget.value = "new";
}

async function onProviderSave(body: ProviderUpsertBody): Promise<void> {
  const target = providerTarget.value;
  const id = target !== null && target !== "new" ? target.id : null;
  providerSaving.value = true;
  try {
    await saveProvider(id, body);
    toast.add({ severity: "success", summary: "Saved", detail: body.name, life: 2500 });
    providerTarget.value = null;
  } catch (error) {
    reportWithValidation(error, "Couldn't save provider");
  } finally {
    providerSaving.value = false;
  }
}

async function onModelSave(body: ModelUpsertBody): Promise<void> {
  const target = modelTarget.value;
  const id = target !== null && target !== "new" ? target.id : null;
  modelSaving.value = true;
  try {
    await saveModel(id, body);
    toast.add({ severity: "success", summary: "Saved", detail: body.label, life: 2500 });
    modelTarget.value = null;
  } catch (error) {
    reportWithValidation(error, "Couldn't save model");
  } finally {
    modelSaving.value = false;
  }
}

async function confirmDeleteProvider(): Promise<void> {
  const target = deleteProviderTarget.value;
  if (target === null) {
    return;
  }
  deleting.value = true;
  try {
    await deleteProvider(target.id);
    toast.add({ severity: "success", summary: "Deleted", detail: target.name, life: 2500 });
    deleteProviderTarget.value = null;
  } catch {
    toast.add({
      severity: "error",
      summary: "Could not delete",
      detail: "Delete this provider's models first.",
      life: 4000,
    });
  } finally {
    deleting.value = false;
  }
}

async function confirmDeleteModel(): Promise<void> {
  const target = deleteModelTarget.value;
  if (target === null) {
    return;
  }
  deleting.value = true;
  try {
    await deleteModel(target.id);
    toast.add({ severity: "success", summary: "Deleted", detail: target.label, life: 2500 });
    deleteModelTarget.value = null;
  } catch {
    toast.add({
      severity: "error",
      summary: "Could not delete",
      detail: "Reassign the tasks using this model first.",
      life: 4000,
    });
  } finally {
    deleting.value = false;
  }
}

async function assignTask(row: ChatTaskRow): Promise<void> {
  try {
    await saveSetting(row.key, row.modelId, "String");
    toast.add({ severity: "success", summary: "Assigned", detail: row.label, life: 2000 });
  } catch (error) {
    report(error);
  }
}

function onEmbeddingChange(): void {
  if (embeddingModelId.value !== embeddingSaved.value) {
    swapModelId.value = embeddingModelId.value;
  }
}

function onSwapClose(): void {
  swapModelId.value = null;
  embeddingModelId.value = embeddingSaved.value;
}

function onSwapApplied(): void {
  // The selects resync from the settings reload that applyEmbeddingSwap did.
  swapModelId.value = null;
}

async function saveTrim(): Promise<void> {
  const max = Number(draftMaxChunks.value);
  const maxLocal = Number(draftMaxChunksLocal.value);
  if (!Number.isInteger(max) || max < 1 || !Number.isInteger(maxLocal) || maxLocal < 1) {
    toast.add({
      severity: "error",
      summary: "Invalid value",
      detail: "Enter whole numbers of 1 or more.",
      life: 4000,
    });
    return;
  }
  savingTrim.value = true;
  try {
    await saveSetting("assistant.evidence.trimLocal", draftTrimLocal.value, "Bool");
    await saveSetting("assistant.evidence.maxChunks", max, "Int");
    await saveSetting("assistant.evidence.maxChunksLocal", maxLocal, "Int");
    toast.add({ severity: "success", summary: "Saved", detail: "Prompt trimming", life: 2500 });
  } catch (error) {
    report(error);
  } finally {
    savingTrim.value = false;
  }
}
</script>

<template>
  <section>
    <AdminPageHeader
      title="AI models"
      description="Define providers and models once, then assign each task to a model. Assignments take effect on the next call — no restart."
    >
      <template #actions>
        <Button label="Add provider" icon="pi pi-plus" size="small" @click="providerTarget = 'new'" />
      </template>
    </AdminPageHeader>

    <p v-if="initialLoading" class="text-muted-color">Loading…</p>

    <div v-else class="space-y-8">
      <!-- Providers & models -->
      <div v-if="sortedProviders.length === 0" class="rounded-border border border-surface p-6 text-center">
        <p class="mb-3 text-muted-color">
          No providers yet. Add one (a local Ollama or any OpenAI-compatible API) to start
          defining models.
        </p>
        <Button label="Add provider" icon="pi pi-plus" size="small" outlined @click="providerTarget = 'new'" />
      </div>
      <div v-else class="space-y-4">
        <ProviderCard
          v-for="provider in sortedProviders"
          :key="provider.id"
          :provider="provider"
          :models="modelsByProvider.get(provider.id) ?? []"
          :assignments="assignments"
          @edit-provider="providerTarget = provider"
          @delete-provider="deleteProviderTarget = provider"
          @add-model="openAddModel(provider)"
          @edit-model="(model) => (modelTarget = model)"
          @delete-model="(model) => (deleteModelTarget = model)"
        />
      </div>

      <!-- Task routing -->
      <div class="rounded-border border border-surface p-4">
        <h3 class="mb-1 font-medium">Task routing</h3>
        <p class="mb-4 text-sm text-muted-color">
          Pick a model per task. Saved instantly on change.
        </p>
        <div class="space-y-3">
          <div
            v-for="row in chatTasks"
            :key="row.key"
            class="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-3"
          >
            <label :for="`task-${row.key}`" class="text-sm sm:w-40 sm:shrink-0">
              {{ row.label }}
            </label>
            <Select
              :input-id="`task-${row.key}`"
              v-model="row.modelId"
              :options="modelOptions"
              option-label="label"
              option-value="value"
              placeholder="Select a model"
              class="w-full sm:w-80"
              @change="assignTask(row)"
            />
          </div>
          <div class="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-3">
            <label for="task-embedding" class="text-sm sm:w-40 sm:shrink-0">Embedding</label>
            <Select
              input-id="task-embedding"
              v-model="embeddingModelId"
              :options="modelOptions"
              option-label="label"
              option-value="value"
              placeholder="Select a model"
              class="w-full sm:w-80"
              @change="onEmbeddingChange"
            />
            <span class="text-xs text-muted-color">Changing this re-embeds every book.</span>
          </div>
        </div>
      </div>

      <!-- Local prompt trimming -->
      <div class="rounded-border border border-surface p-4">
        <h3 class="mb-1 font-medium">Local prompt trimming</h3>
        <p class="mb-4 text-sm text-muted-color">
          Use a smaller evidence set in the Q&amp;A prompt when it runs on a local (Ollama) model,
          to speed up CPU inference. Hosted models always get the full set.
        </p>
        <div class="mb-4 flex items-center gap-2">
          <Checkbox v-model="draftTrimLocal" binary input-id="trimLocal" />
          <label for="trimLocal" class="text-sm">Trim the prompt for local models</label>
        </div>
        <div class="mb-3 flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-3">
          <label for="trim-max-chunks" class="text-sm sm:w-56 sm:shrink-0">
            Evidence chunks (hosted)
          </label>
          <InputText
            id="trim-max-chunks"
            v-model="draftMaxChunks"
            class="w-24"
            inputmode="numeric"
          />
        </div>
        <div class="mb-4 flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-3">
          <label for="trim-max-chunks-local" class="text-sm sm:w-56 sm:shrink-0">
            Evidence chunks (local)
          </label>
          <InputText
            id="trim-max-chunks-local"
            v-model="draftMaxChunksLocal"
            class="w-24"
            inputmode="numeric"
            :disabled="!draftTrimLocal"
          />
        </div>
        <Button label="Save" size="small" :loading="savingTrim" @click="saveTrim" />
      </div>
    </div>

    <ProviderDialog
      :target="providerTarget"
      :saving="providerSaving"
      @save="onProviderSave"
      @close="providerTarget = null"
    />

    <ModelDialog
      :target="modelTarget"
      :providers="sortedProviders"
      :default-provider-id="modelDefaultProviderId"
      :saving="modelSaving"
      @save="onModelSave"
      @close="modelTarget = null"
    />

    <EmbeddingSwapDialog
      :model-id="swapModelId"
      :model-label="swapModelLabel"
      @applied="onSwapApplied"
      @close="onSwapClose"
    />

    <Dialog
      :visible="deleteProviderTarget !== null"
      modal
      header="Delete provider"
      :closable="!deleting"
      class="w-[28rem] max-w-[calc(100vw-2rem)]"
      @update:visible="
        (open) => {
          if (!open) deleteProviderTarget = null;
        }
      "
    >
      <p v-if="deleteProviderModelCount > 0" class="text-sm">
        <strong>{{ deleteProviderTarget?.name }}</strong> still has
        {{ deleteProviderModelCount }} model(s). Delete or move them first — a provider with
        models can't be removed.
      </p>
      <p v-else class="text-sm">
        Delete <strong>{{ deleteProviderTarget?.name }}</strong
        >? Its stored API key is removed with it.
      </p>
      <template #footer>
        <Button label="Cancel" text :disabled="deleting" @click="deleteProviderTarget = null" />
        <Button
          label="Delete"
          severity="danger"
          :loading="deleting"
          :disabled="deleteProviderModelCount > 0"
          @click="confirmDeleteProvider"
        />
      </template>
    </Dialog>

    <Dialog
      :visible="deleteModelTarget !== null"
      modal
      header="Delete model"
      :closable="!deleting"
      class="w-[28rem] max-w-[calc(100vw-2rem)]"
      @update:visible="
        (open) => {
          if (!open) deleteModelTarget = null;
        }
      "
    >
      <p v-if="deleteModelTasks.length > 0" class="text-sm">
        <strong>{{ deleteModelTarget?.label }}</strong> is assigned to
        {{ deleteModelTasks.join(", ") }}. Reassign those first — an assigned model can't be
        removed.
      </p>
      <p v-else class="text-sm">
        Delete <strong>{{ deleteModelTarget?.label }}</strong
        >?
      </p>
      <template #footer>
        <Button label="Cancel" text :disabled="deleting" @click="deleteModelTarget = null" />
        <Button
          label="Delete"
          severity="danger"
          :loading="deleting"
          :disabled="deleteModelTasks.length > 0"
          @click="confirmDeleteModel"
        />
      </template>
    </Dialog>
  </section>
</template>

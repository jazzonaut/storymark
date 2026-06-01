<script setup lang="ts">
import Button from "primevue/button";
import Checkbox from "primevue/checkbox";
import Dialog from "primevue/dialog";
import InputText from "primevue/inputtext";
import Password from "primevue/password";
import Select from "primevue/select";
import SelectButton from "primevue/selectbutton";
import { useToast } from "primevue/usetoast";
import { computed, onMounted, ref, watch } from "vue";
import type { EmbeddingImpactPreview, LlmProviderKind } from "@/api/admin-types";
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
};

const KIND_OPTIONS: { label: string; value: LlmProviderKind }[] = [
  { label: "Ollama (local)", value: "ollama" },
  { label: "OpenAI-compatible", value: "openai_compatible" },
];

interface ProviderDraft {
  id: string | null;
  name: string;
  kind: LlmProviderKind;
  baseUrl: string;
  apiKey: string;
  hasApiKey: boolean;
}

interface ModelDraft {
  id: string | null;
  label: string;
  providerId: string;
  modelName: string;
  input: string;
  output: string;
}

interface ChatTaskRow {
  key: string;
  label: string;
  modelId: string;
}

const {
  settings,
  providers,
  models,
  isLoading,
  loadSettings,
  saveSetting,
  loadProviders,
  saveProvider,
  deleteProvider,
  loadModels,
  saveModel,
  deleteModel,
  previewEmbeddingSwap,
  applyEmbeddingSwap,
} = useAdmin();
const { report } = useAdminError();
const toast = useToast();

const providerDrafts = ref<ProviderDraft[]>([]);
const modelDrafts = ref<ModelDraft[]>([]);
const chatTasks = ref<ChatTaskRow[]>([]);
const embeddingModelId = ref("");
const embeddingSaved = ref("");
const draftTrimLocal = ref(true);
const draftMaxChunks = ref("12");
const draftMaxChunksLocal = ref("6");

const providerOptions = computed(() =>
  providers.value.map((p) => ({ label: p.name, value: p.id })),
);
const modelOptions = computed(() => models.value.map((m) => ({ label: m.label, value: m.id })));

interface SwapState {
  visible: boolean;
  modelId: string;
  preview: EmbeddingImpactPreview | null;
  loading: boolean;
  applying: boolean;
}
const swap = ref<SwapState>({
  visible: false,
  modelId: "",
  preview: null,
  loading: false,
  applying: false,
});

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
  providers,
  () => {
    providerDrafts.value = providers.value.map((p) => ({
      id: p.id,
      name: p.name,
      kind: p.kind,
      baseUrl: p.baseUrl,
      apiKey: "",
      hasApiKey: p.hasApiKey,
    }));
  },
  { immediate: true },
);

watch(
  models,
  () => {
    modelDrafts.value = models.value.map((m) => ({
      id: m.id,
      label: m.label,
      providerId: m.providerId,
      modelName: m.modelName,
      input: m.inputPerMillion === null ? "" : String(m.inputPerMillion),
      output: m.outputPerMillion === null ? "" : String(m.outputPerMillion),
    }));
  },
  { immediate: true },
);

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

function addProvider(): void {
  providerDrafts.value.push({
    id: null,
    name: "",
    kind: "ollama",
    baseUrl: "",
    apiKey: "",
    hasApiKey: false,
  });
}

async function saveProviderRow(draft: ProviderDraft): Promise<void> {
  if (draft.name.trim().length === 0 || draft.baseUrl.trim().length === 0) {
    toast.add({
      severity: "error",
      summary: "Missing field",
      detail: "Name and base URL are required.",
      life: 4000,
    });
    return;
  }
  try {
    await saveProvider(draft.id, {
      name: draft.name.trim(),
      kind: draft.kind,
      baseUrl: draft.baseUrl.trim(),
      apiKey: draft.apiKey.length > 0 ? draft.apiKey : null,
    });
    toast.add({ severity: "success", summary: "Saved", detail: draft.name.trim(), life: 2500 });
  } catch (error) {
    report(error);
  }
}

async function deleteProviderRow(draft: ProviderDraft): Promise<void> {
  if (draft.id === null) {
    providerDrafts.value = providerDrafts.value.filter((d) => d !== draft);
    return;
  }
  try {
    await deleteProvider(draft.id);
    toast.add({ severity: "success", summary: "Deleted", detail: draft.name, life: 2500 });
  } catch {
    toast.add({
      severity: "error",
      summary: "Could not delete",
      detail: "Delete this provider's models first.",
      life: 4000,
    });
  }
}

function addModel(): void {
  modelDrafts.value.push({
    id: null,
    label: "",
    providerId: providers.value[0]?.id ?? "",
    modelName: "",
    input: "",
    output: "",
  });
}

function parseCost(value: string): number | null | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return null;
  }
  const numeric = Number(trimmed);
  return Number.isFinite(numeric) && numeric >= 0 ? numeric : undefined;
}

async function saveModelRow(draft: ModelDraft): Promise<void> {
  if (draft.label.trim().length === 0 || draft.modelName.trim().length === 0) {
    toast.add({
      severity: "error",
      summary: "Missing field",
      detail: "Label and model name are required.",
      life: 4000,
    });
    return;
  }
  if (draft.providerId.length === 0) {
    toast.add({
      severity: "error",
      summary: "Missing provider",
      detail: "Pick a provider.",
      life: 4000,
    });
    return;
  }
  const input = parseCost(draft.input);
  const output = parseCost(draft.output);
  if (input === undefined || output === undefined) {
    toast.add({
      severity: "error",
      summary: "Invalid cost",
      detail: "Costs must be non-negative numbers.",
      life: 4000,
    });
    return;
  }
  try {
    await saveModel(draft.id, {
      label: draft.label.trim(),
      providerId: draft.providerId,
      modelName: draft.modelName.trim(),
      inputPerMillion: input,
      outputPerMillion: output,
    });
    toast.add({ severity: "success", summary: "Saved", detail: draft.label.trim(), life: 2500 });
  } catch (error) {
    report(error);
  }
}

async function deleteModelRow(draft: ModelDraft): Promise<void> {
  if (draft.id === null) {
    modelDrafts.value = modelDrafts.value.filter((d) => d !== draft);
    return;
  }
  try {
    await deleteModel(draft.id);
    toast.add({ severity: "success", summary: "Deleted", detail: draft.label, life: 2500 });
  } catch {
    toast.add({
      severity: "error",
      summary: "Could not delete",
      detail: "Reassign the tasks using this model first.",
      life: 4000,
    });
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

async function onEmbeddingChange(): Promise<void> {
  if (embeddingModelId.value === embeddingSaved.value) {
    return;
  }
  swap.value = {
    visible: true,
    modelId: embeddingModelId.value,
    preview: null,
    loading: true,
    applying: false,
  };
  try {
    swap.value.preview = await previewEmbeddingSwap();
  } catch (error) {
    swap.value.visible = false;
    embeddingModelId.value = embeddingSaved.value;
    report(error);
  } finally {
    swap.value.loading = false;
  }
}

function cancelSwap(): void {
  swap.value.visible = false;
  embeddingModelId.value = embeddingSaved.value;
}

async function confirmSwap(): Promise<void> {
  swap.value.applying = true;
  try {
    const result = await applyEmbeddingSwap(swap.value.modelId);
    toast.add({
      severity: "success",
      summary: "Reindex started",
      detail: `${result.booksQueued} book(s) queued.`,
      life: 4000,
    });
    swap.value.visible = false;
  } catch (error) {
    embeddingModelId.value = embeddingSaved.value;
    report(error);
  } finally {
    swap.value.applying = false;
  }
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
  try {
    await saveSetting("assistant.evidence.trimLocal", draftTrimLocal.value, "Bool");
    await saveSetting("assistant.evidence.maxChunks", max, "Int");
    await saveSetting("assistant.evidence.maxChunksLocal", maxLocal, "Int");
    toast.add({ severity: "success", summary: "Saved", detail: "Prompt trimming", life: 2500 });
  } catch (error) {
    report(error);
  }
}

function formatDuration(seconds: number): string {
  return seconds < 60 ? `~${seconds}s` : `~${Math.round(seconds / 60)} min`;
}

onMounted(() => {
  void Promise.all([loadProviders(), loadModels(), loadSettings()]).catch(report);
});
</script>

<template>
  <section class="max-w-3xl space-y-10" :class="{ 'opacity-50': isLoading }">
    <div>
      <h2 class="mb-1 text-lg font-semibold">AI models</h2>
      <p class="text-sm opacity-70">
        Define providers and models once, then assign each task to a model. Assignments take effect
        on the next call — no restart.
      </p>
    </div>

    <!-- Providers -->
    <div>
      <div class="mb-3 flex items-center justify-between">
        <h3 class="font-medium">Providers</h3>
        <Button label="Add provider" size="small" outlined @click="addProvider" />
      </div>
      <p v-if="providerDrafts.length === 0" class="text-sm opacity-60">No providers yet.</p>
      <div
        v-for="(draft, index) in providerDrafts"
        :key="draft.id ?? `new-${index}`"
        class="mb-3 rounded-lg border border-zinc-200 p-4 dark:border-zinc-800"
      >
        <div class="flex flex-wrap items-end gap-3">
          <div>
            <label class="mb-1 block text-xs opacity-70">Name</label>
            <InputText v-model="draft.name" placeholder="OpenRouter" class="w-44" />
          </div>
          <div>
            <label class="mb-1 block text-xs opacity-70">Kind</label>
            <SelectButton
              v-model="draft.kind"
              :options="KIND_OPTIONS"
              option-label="label"
              option-value="value"
              :allow-empty="false"
            />
          </div>
          <Button
            label="Delete"
            size="small"
            severity="danger"
            text
            @click="deleteProviderRow(draft)"
          />
          <Button label="Save" size="small" @click="saveProviderRow(draft)" />
        </div>
        <div class="mt-3 flex flex-wrap items-end gap-3">
          <div class="min-w-72 flex-1">
            <label class="mb-1 block text-xs opacity-70">Base URL</label>
            <InputText v-model="draft.baseUrl" placeholder="https://openrouter.ai/api/v1" fluid />
          </div>
          <div class="min-w-64 flex-1">
            <label class="mb-1 block text-xs opacity-70">
              API key
              <span v-if="draft.hasApiKey" class="opacity-60">(stored — type to replace)</span>
            </label>
            <Password
              v-model="draft.apiKey"
              :feedback="false"
              toggle-mask
              fluid
              placeholder="Leave blank to keep / none for local"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Models -->
    <div>
      <div class="mb-3 flex items-center justify-between">
        <h3 class="font-medium">Models</h3>
        <Button
          label="Add model"
          size="small"
          outlined
          :disabled="providers.length === 0"
          @click="addModel"
        />
      </div>
      <p v-if="modelDrafts.length === 0" class="text-sm opacity-60">No models yet.</p>
      <div
        v-for="(draft, index) in modelDrafts"
        :key="draft.id ?? `new-${index}`"
        class="mb-3 rounded-lg border border-zinc-200 p-4 dark:border-zinc-800"
      >
        <div class="flex flex-wrap items-end gap-3">
          <div>
            <label class="mb-1 block text-xs opacity-70">Label</label>
            <InputText v-model="draft.label" placeholder="GPT-4o mini" class="w-44" />
          </div>
          <div>
            <label class="mb-1 block text-xs opacity-70">Provider</label>
            <Select
              v-model="draft.providerId"
              :options="providerOptions"
              option-label="label"
              option-value="value"
              placeholder="Provider"
              class="w-44"
            />
          </div>
          <div class="min-w-56 flex-1">
            <label class="mb-1 block text-xs opacity-70">Model name</label>
            <InputText v-model="draft.modelName" placeholder="openai/gpt-4o-mini" fluid />
          </div>
        </div>
        <div class="mt-3 flex flex-wrap items-end gap-3">
          <div>
            <label class="mb-1 block text-xs opacity-70">Cost in ($/1M)</label>
            <InputText v-model="draft.input" class="w-28" inputmode="decimal" placeholder="0.15" />
          </div>
          <div>
            <label class="mb-1 block text-xs opacity-70">Cost out ($/1M)</label>
            <InputText v-model="draft.output" class="w-28" inputmode="decimal" placeholder="0.60" />
          </div>
          <div class="flex-1"></div>
          <Button
            label="Delete"
            size="small"
            severity="danger"
            text
            @click="deleteModelRow(draft)"
          />
          <Button label="Save" size="small" @click="saveModelRow(draft)" />
        </div>
      </div>
    </div>

    <!-- Task assignments -->
    <div class="rounded-lg border border-zinc-200 p-4 dark:border-zinc-800">
      <h3 class="mb-1 font-medium">Task assignments</h3>
      <p class="mb-4 text-sm opacity-70">Pick a model per task. Saved instantly on change.</p>
      <div v-for="row in chatTasks" :key="row.key" class="mb-2 flex items-center gap-3">
        <label class="w-40 text-sm">{{ row.label }}</label>
        <Select
          v-model="row.modelId"
          :options="modelOptions"
          option-label="label"
          option-value="value"
          placeholder="Select a model"
          class="w-72"
          @change="assignTask(row)"
        />
      </div>
      <div class="mt-2 flex items-center gap-3">
        <label class="w-40 text-sm">Embedding</label>
        <Select
          v-model="embeddingModelId"
          :options="modelOptions"
          option-label="label"
          option-value="value"
          placeholder="Select a model"
          class="w-72"
          @change="onEmbeddingChange"
        />
        <span class="text-xs opacity-60">Changing this re-embeds every book.</span>
      </div>
    </div>

    <!-- Local prompt trimming -->
    <div class="rounded-lg border border-zinc-200 p-4 dark:border-zinc-800">
      <h3 class="mb-1 font-medium">Local prompt trimming</h3>
      <p class="mb-4 text-sm opacity-70">
        Use a smaller evidence set in the Q&amp;A prompt when it runs on a local (Ollama) model, to
        speed up CPU inference. Hosted models always get the full set.
      </p>
      <div class="mb-4 flex items-center gap-2">
        <Checkbox v-model="draftTrimLocal" binary input-id="trimLocal" />
        <label for="trimLocal" class="text-sm">Trim the prompt for local models</label>
      </div>
      <div class="mb-3 flex items-center gap-3">
        <label class="w-56 text-sm">Evidence chunks (hosted)</label>
        <InputText v-model="draftMaxChunks" class="w-24" inputmode="numeric" />
      </div>
      <div class="mb-4 flex items-center gap-3">
        <label class="w-56 text-sm">Evidence chunks (local)</label>
        <InputText
          v-model="draftMaxChunksLocal"
          class="w-24"
          inputmode="numeric"
          :disabled="!draftTrimLocal"
        />
      </div>
      <Button label="Save" size="small" @click="saveTrim" />
    </div>

    <Dialog
      v-model:visible="swap.visible"
      modal
      header="Change embedding model"
      :closable="!swap.applying"
      :style="{ width: '32rem' }"
      @hide="cancelSwap"
    >
      <p class="mb-3">
        Switching the embedding model re-embeds every chunk under the new model. Search results for
        a book are incomplete until its reindex finishes.
      </p>
      <p v-if="swap.loading" class="opacity-70">Estimating impact…</p>
      <ul v-else-if="swap.preview" class="mb-3 list-disc pl-5">
        <li>{{ swap.preview.booksAffected }} book(s) will be reindexed</li>
        <li>{{ swap.preview.chunkCount }} chunk(s) to re-embed</li>
        <li>Estimated time: {{ formatDuration(swap.preview.estimatedSeconds) }}</li>
      </ul>
      <template #footer>
        <Button label="Cancel" text :disabled="swap.applying" @click="cancelSwap" />
        <Button
          label="Reindex"
          :loading="swap.applying"
          :disabled="swap.loading"
          @click="confirmSwap"
        />
      </template>
    </Dialog>
  </section>
</template>

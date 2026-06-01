<script setup lang="ts">
import Button from "primevue/button";
import Checkbox from "primevue/checkbox";
import InputText from "primevue/inputtext";
import Password from "primevue/password";
import SelectButton from "primevue/selectbutton";
import { useToast } from "primevue/usetoast";
import { onMounted, ref, watch } from "vue";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";

const TASK_PREFIX = "llm.chat.taskMap.";

// Friendly names for the known chat tasks; an unknown task falls back to its id.
const TASK_LABELS: Record<string, string> = {
  qa: "Q&A",
  extract: "Extraction",
  entity_summary: "Entity summary",
  selection_hint: "Selection hint",
  memory: "Memory",
};

const PROVIDERS = [
  { label: "Ollama (local)", value: "ollama" },
  { label: "OpenAI-compatible", value: "openai_compatible" },
] as const;

interface TaskRow {
  key: string;
  task: string;
  label: string;
  provider: string;
  model: string;
}

const { settings, isLoading, loadSettings, saveSetting } = useAdmin();
const { report } = useAdminError();
const toast = useToast();

const taskRows = ref<TaskRow[]>([]);
const draftBaseUrl = ref("");
const draftApiKey = ref("");
const draftTrimLocal = ref(true);
const draftMaxChunks = ref("12");
const draftMaxChunksLocal = ref("6");

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

// Rebuild every draft from the loaded settings. Runs whenever the settings list
// is reloaded (each save reloads), so a row reflects the persisted value.
function syncFromSettings(): void {
  taskRows.value = settings.value
    .filter((s) => s.key.startsWith(TASK_PREFIX))
    .map((s) => {
      const task = s.key.slice(TASK_PREFIX.length);
      const raw = parsedString(s.value);
      const colon = raw.indexOf(":");
      return {
        key: s.key,
        task,
        label: TASK_LABELS[task] ?? task,
        provider: colon > 0 ? raw.slice(0, colon) : "ollama",
        model: colon > 0 ? raw.slice(colon + 1) : raw,
      };
    })
    .toSorted((a, b) => a.task.localeCompare(b.task));

  const baseUrl = settingValue("llm.openai.baseUrl");
  draftBaseUrl.value = baseUrl === null ? "" : parsedString(baseUrl);

  const trim = settingValue("assistant.evidence.trimLocal");
  draftTrimLocal.value = trim === null ? true : parsedBool(trim);
  const maxChunks = settingValue("assistant.evidence.maxChunks");
  draftMaxChunks.value = maxChunks === null ? "12" : parsedString(maxChunks);
  const maxLocal = settingValue("assistant.evidence.maxChunksLocal");
  draftMaxChunksLocal.value = maxLocal === null ? "6" : parsedString(maxLocal);
}

watch(settings, syncFromSettings, { immediate: true });

async function saveTask(row: TaskRow): Promise<void> {
  const model = row.model.trim();
  if (model.length === 0) {
    toast.add({ severity: "error", summary: "Model required", detail: row.label, life: 4000 });
    return;
  }
  try {
    await saveSetting(row.key, `${row.provider}:${model}`, "String");
    toast.add({ severity: "success", summary: "Saved", detail: row.label, life: 2500 });
  } catch (error) {
    report(error);
  }
}

async function saveBaseUrl(): Promise<void> {
  try {
    await saveSetting("llm.openai.baseUrl", draftBaseUrl.value.trim(), "String");
    toast.add({ severity: "success", summary: "Saved", detail: "Base URL", life: 2500 });
  } catch (error) {
    report(error);
  }
}

async function saveApiKey(): Promise<void> {
  if (draftApiKey.value.length === 0) {
    return;
  }
  try {
    await saveSetting("llm.openai.apiKey", draftApiKey.value, "String");
    draftApiKey.value = "";
    toast.add({ severity: "success", summary: "Saved", detail: "API key", life: 2500 });
  } catch (error) {
    report(error);
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

onMounted(() => {
  void loadSettings().catch(report);
});
</script>

<template>
  <section class="max-w-3xl space-y-8">
    <div>
      <h2 class="mb-1 text-lg font-semibold">AI models</h2>
      <p class="text-sm opacity-70">
        Route each task to a local Ollama model or a hosted OpenAI-compatible model. Changes take
        effect on the next call — no restart.
      </p>
    </div>

    <div class="space-y-3" :class="{ 'opacity-50': isLoading }">
      <div
        v-for="row in taskRows"
        :key="row.key"
        class="rounded-lg border border-zinc-200 p-4 dark:border-zinc-800"
      >
        <div class="mb-2 text-sm font-medium">
          {{ row.label }} <span class="opacity-50">({{ row.task }})</span>
        </div>
        <div class="flex flex-wrap items-center gap-3">
          <SelectButton
            v-model="row.provider"
            :options="[...PROVIDERS]"
            option-label="label"
            option-value="value"
            :allow-empty="false"
          />
          <InputText v-model="row.model" placeholder="model name" class="min-w-52 flex-1" />
          <Button label="Save" size="small" @click="saveTask(row)" />
        </div>
      </div>
    </div>

    <div class="rounded-lg border border-zinc-200 p-4 dark:border-zinc-800">
      <h3 class="mb-1 font-medium">OpenAI-compatible endpoint</h3>
      <p class="mb-4 text-sm opacity-70">
        Required when any task above uses OpenAI-compatible. Works with OpenAI, Groq, OpenRouter, or
        a self-hosted server. The key is stored encrypted and write-only.
      </p>

      <label class="mb-1 block text-sm font-medium">Base URL</label>
      <div class="mb-4 flex items-center gap-2">
        <InputText v-model="draftBaseUrl" placeholder="https://api.openai.com/v1" fluid />
        <Button label="Save" size="small" @click="saveBaseUrl" />
      </div>

      <label class="mb-1 block text-sm font-medium">API key</label>
      <div class="flex items-center gap-2">
        <Password
          v-model="draftApiKey"
          :feedback="false"
          toggle-mask
          fluid
          placeholder="Type a new key (current value is hidden)"
        />
        <Button label="Save" size="small" :disabled="draftApiKey.length === 0" @click="saveApiKey" />
      </div>
    </div>

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
  </section>
</template>

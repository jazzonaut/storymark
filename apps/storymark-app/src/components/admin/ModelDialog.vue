<script setup lang="ts">
import Button from "primevue/button";
import InputText from "primevue/inputtext";
import Select from "primevue/select";
import { computed, ref, watch } from "vue";
import type { ModelItem, ModelUpsertBody, ProviderItem, ReasoningEffort } from "@/api/admin-types";
import AdminDialog from "@/components/admin/AdminDialog.vue";

const props = defineProps<{
  /** The model to edit, `"new"` to create, or null when closed. */
  target: ModelItem | "new" | null;
  providers: readonly ProviderItem[];
  /** Preselected provider when creating from a provider card. */
  defaultProviderId: string | null;
  saving: boolean;
}>();

const emit = defineEmits<{
  (e: "save", body: ModelUpsertBody): void;
  (e: "close"): void;
}>();

const label = ref("");
const providerId = ref("");
const modelName = ref("");
const inputCost = ref("");
const outputCost = ref("");
const reasoningEffort = ref<ReasoningEffort | "">("");

const visible = computed<boolean>(() => props.target !== null);
const isNew = computed<boolean>(() => props.target === "new");
const selectedProvider = computed(() =>
  props.providers.find((provider) => provider.id === providerId.value),
);
const supportsReasoning = computed<boolean>(
  () => selectedProvider.value?.kind === "openai_compatible",
);
const isOpenRouter = computed<boolean>(() => {
  if (!supportsReasoning.value || selectedProvider.value === undefined) {
    return false;
  }
  try {
    const host = new URL(selectedProvider.value.baseUrl).hostname;
    return host === "openrouter.ai" || host.endsWith(".openrouter.ai");
  } catch {
    return false;
  }
});
const reasoningOptions = [
  { label: "Provider default", value: "" },
  { label: "Off", value: "none" },
  { label: "Minimal", value: "minimal" },
  { label: "Low", value: "low" },
  { label: "Medium", value: "medium" },
  { label: "High", value: "high" },
  { label: "Extra high", value: "xhigh" },
  { label: "Maximum", value: "max" },
];

const providerOptions = computed(() =>
  props.providers.map((provider) => ({ label: provider.name, value: provider.id })),
);

/** null for blank (no rate configured), the number when valid, undefined when invalid. */
function parseCost(value: string): number | null | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return null;
  }
  const numeric = Number(trimmed);
  return Number.isFinite(numeric) && numeric >= 0 ? numeric : undefined;
}

const costsValid = computed<boolean>(
  () => parseCost(inputCost.value) !== undefined && parseCost(outputCost.value) !== undefined,
);
const canSave = computed<boolean>(
  () =>
    label.value.trim().length > 0 &&
    modelName.value.trim().length > 0 &&
    providerId.value.length > 0 &&
    costsValid.value,
);

watch(
  () => props.target,
  (target) => {
    if (target === null) {
      return;
    }
    if (target === "new") {
      label.value = "";
      providerId.value = props.defaultProviderId ?? props.providers[0]?.id ?? "";
      modelName.value = "";
      inputCost.value = "";
      outputCost.value = "";
      reasoningEffort.value = "";
      if (isOpenRouter.value) {
        useLunaPreset();
      }
      return;
    }
    label.value = target.label;
    providerId.value = target.providerId;
    modelName.value = target.modelName;
    inputCost.value = target.inputPerMillion === null ? "" : String(target.inputPerMillion);
    outputCost.value = target.outputPerMillion === null ? "" : String(target.outputPerMillion);
    reasoningEffort.value = target.reasoningEffort ?? "";
  },
);

function useLunaPreset(): void {
  label.value = "GPT-6 Luna";
  modelName.value = "openai/gpt-6-luna";
  inputCost.value = "0.10";
  outputCost.value = "0.50";
  reasoningEffort.value = "none";
}

function save(): void {
  const input = parseCost(inputCost.value);
  const output = parseCost(outputCost.value);
  // `saving` guards the Enter-key path; the footer button is already disabled.
  if (props.saving || !canSave.value || input === undefined || output === undefined) {
    return;
  }
  emit("save", {
    label: label.value.trim(),
    providerId: providerId.value,
    modelName: modelName.value.trim(),
    reasoningEffort:
      supportsReasoning.value && reasoningEffort.value !== "" ? reasoningEffort.value : null,
    inputPerMillion: input,
    outputPerMillion: output,
  });
}
</script>

<template>
  <AdminDialog
    :visible="visible"
    :header="isNew ? 'Add model' : 'Edit model'"
    :busy="saving"
    @close="emit('close')"
  >
    <div class="space-y-4">
      <div>
        <label for="model-label" class="mb-1 block text-sm text-muted-color">Label</label>
        <InputText
          id="model-label"
          v-model="label"
          placeholder="GPT-6 Luna"
          fluid
          autofocus
          @keyup.enter="save"
        />
      </div>
      <div>
        <label for="model-provider" class="mb-1 block text-sm text-muted-color">Provider</label>
        <Select
          id="model-provider"
          v-model="providerId"
          :options="providerOptions"
          option-label="label"
          option-value="value"
          placeholder="Select a provider"
          fluid
        />
      </div>
      <Button
        v-if="isOpenRouter"
        label="Use GPT-6 Luna defaults"
        size="small"
        text
        :disabled="saving"
        @click="useLunaPreset"
      />
      <div>
        <label for="model-name" class="mb-1 block text-sm text-muted-color">Model name</label>
        <InputText
          id="model-name"
          v-model="modelName"
          placeholder="openai/gpt-6-luna"
          class="font-mono"
          fluid
          @keyup.enter="save"
        />
      </div>
      <div v-if="supportsReasoning">
        <label for="model-reasoning" class="mb-1 block text-sm text-muted-color">Reasoning</label>
        <Select
          input-id="model-reasoning"
          v-model="reasoningEffort"
          :options="reasoningOptions"
          option-label="label"
          option-value="value"
          fluid
        />
        <p class="mt-1 text-sm text-muted-color">
          Off is recommended for Luna's fastest replies. Higher effort can increase time and cost.
          Other models may support fewer levels; use Provider default if unsure.
        </p>
      </div>
      <div class="grid grid-cols-2 gap-3">
        <div>
          <label for="model-cost-in" class="mb-1 block text-sm text-muted-color">
            Cost in ($/1M)
          </label>
          <InputText
            id="model-cost-in"
            v-model="inputCost"
            inputmode="decimal"
            placeholder="0.10"
            fluid
            @keyup.enter="save"
          />
        </div>
        <div>
          <label for="model-cost-out" class="mb-1 block text-sm text-muted-color">
            Cost out ($/1M)
          </label>
          <InputText
            id="model-cost-out"
            v-model="outputCost"
            inputmode="decimal"
            placeholder="0.50"
            fluid
            @keyup.enter="save"
          />
        </div>
      </div>
      <p v-if="!costsValid" class="text-sm text-red-600 dark:text-red-300">
        Costs must be non-negative numbers; leave blank when there's no rate.
      </p>
      <p v-else class="text-sm text-muted-color">
        Blank costs mean no rate is configured (e.g. local models) — the cost meter shows no
        estimate for them.
      </p>
    </div>

    <template #footer>
      <Button label="Cancel" text :disabled="saving" @click="emit('close')" />
      <Button
        :label="isNew ? 'Add model' : 'Save'"
        :loading="saving"
        :disabled="!canSave"
        @click="save"
      />
    </template>
  </AdminDialog>
</template>

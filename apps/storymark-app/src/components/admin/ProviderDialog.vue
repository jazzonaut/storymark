<script setup lang="ts">
import Button from "primevue/button";
import Dialog from "primevue/dialog";
import InputText from "primevue/inputtext";
import Password from "primevue/password";
import SelectButton from "primevue/selectbutton";
import { computed, ref, watch } from "vue";
import type { LlmProviderKind, ProviderItem, ProviderUpsertBody } from "@/api/admin-types";

const props = defineProps<{
  /** The provider to edit, `"new"` to create, or null when closed. */
  target: ProviderItem | "new" | null;
  saving: boolean;
}>();

const emit = defineEmits<{
  (e: "save", body: ProviderUpsertBody): void;
  (e: "close"): void;
}>();

const KIND_OPTIONS: { label: string; value: LlmProviderKind }[] = [
  { label: "Ollama (local)", value: "ollama" },
  { label: "OpenAI-compatible", value: "openai_compatible" },
];

const name = ref("");
const kind = ref<LlmProviderKind>("ollama");
const baseUrl = ref("");
const apiKey = ref("");
const hasStoredKey = ref(false);

const visible = computed<boolean>(() => props.target !== null);
const isNew = computed<boolean>(() => props.target === "new");
const canSave = computed<boolean>(() => name.value.trim().length > 0 && baseUrl.value.trim().length > 0);

watch(
  () => props.target,
  (target) => {
    if (target === null) {
      return;
    }
    if (target === "new") {
      name.value = "";
      kind.value = "ollama";
      baseUrl.value = "";
      apiKey.value = "";
      hasStoredKey.value = false;
      return;
    }
    name.value = target.name;
    kind.value = target.kind;
    baseUrl.value = target.baseUrl;
    apiKey.value = "";
    hasStoredKey.value = target.hasApiKey;
  },
);

function save(): void {
  if (!canSave.value) {
    return;
  }
  emit("save", {
    name: name.value.trim(),
    kind: kind.value,
    baseUrl: baseUrl.value.trim(),
    apiKey: apiKey.value.length > 0 ? apiKey.value : null,
  });
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="isNew ? 'Add provider' : 'Edit provider'"
    :closable="!saving"
    class="w-[28rem] max-w-[calc(100vw-2rem)]"
    @update:visible="
      (open) => {
        if (!open) emit('close');
      }
    "
  >
    <div class="space-y-4">
      <div>
        <label for="provider-name" class="mb-1 block text-sm text-muted-color">Name</label>
        <InputText
          id="provider-name"
          v-model="name"
          placeholder="OpenRouter"
          fluid
          autofocus
          @keyup.enter="save"
        />
      </div>
      <div>
        <span class="mb-1 block text-sm text-muted-color">Kind</span>
        <SelectButton
          v-model="kind"
          :options="KIND_OPTIONS"
          option-label="label"
          option-value="value"
          :allow-empty="false"
        />
      </div>
      <div>
        <label for="provider-base-url" class="mb-1 block text-sm text-muted-color">Base URL</label>
        <InputText
          id="provider-base-url"
          v-model="baseUrl"
          placeholder="https://openrouter.ai/api/v1"
          fluid
          @keyup.enter="save"
        />
      </div>
      <div>
        <label for="provider-api-key" class="mb-1 block text-sm text-muted-color">
          API key
          <span v-if="hasStoredKey" class="opacity-70">(stored — type to replace)</span>
        </label>
        <Password
          id="provider-api-key"
          v-model="apiKey"
          :feedback="false"
          toggle-mask
          fluid
          :placeholder="hasStoredKey ? 'Leave blank to keep the stored key' : 'None for local'"
        />
      </div>
    </div>

    <template #footer>
      <Button label="Cancel" text :disabled="saving" @click="emit('close')" />
      <Button
        :label="isNew ? 'Add provider' : 'Save'"
        :loading="saving"
        :disabled="!canSave"
        @click="save"
      />
    </template>
  </Dialog>
</template>

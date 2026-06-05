<script setup lang="ts">
import Button from "primevue/button";
import Tag from "primevue/tag";
import type { ModelItem, ProviderItem } from "@/api/admin-types";

const props = defineProps<{
  provider: ProviderItem;
  /** This provider's models only, pre-sorted by the parent. */
  models: readonly ModelItem[];
  /** Task labels (and "Embedding") per model id, for the in-use badges. */
  assignments: ReadonlyMap<string, readonly string[]>;
}>();

defineEmits<{
  (e: "edit-provider"): void;
  (e: "delete-provider"): void;
  (e: "add-model"): void;
  (e: "edit-model", model: ModelItem): void;
  (e: "delete-model", model: ModelItem): void;
}>();

function kindLabel(provider: ProviderItem): string {
  return provider.kind === "ollama" ? "Ollama" : "OpenAI-compatible";
}

function costPart(value: number | null): string {
  return value === null ? "—" : `$${String(value)}`;
}

function formatCosts(model: ModelItem): string {
  if (model.inputPerMillion === null && model.outputPerMillion === null) {
    return "no rates";
  }
  return `${costPart(model.inputPerMillion)} / ${costPart(model.outputPerMillion)} per 1M`;
}

function tasksFor(model: ModelItem): readonly string[] {
  return props.assignments.get(model.id) ?? [];
}
</script>

<template>
  <div class="rounded-border border border-surface">
    <div class="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-surface p-3 sm:p-4">
      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-center gap-2">
          <span class="font-medium">{{ provider.name }}</span>
          <Tag :value="kindLabel(provider)" severity="secondary" />
          <Tag v-if="provider.hasApiKey" value="key stored" severity="info" />
        </div>
        <div class="mt-0.5 truncate font-mono text-xs text-muted-color">{{ provider.baseUrl }}</div>
      </div>
      <div class="flex shrink-0 items-center">
        <Button
          icon="pi pi-pencil"
          text
          size="small"
          severity="secondary"
          :aria-label="`Edit provider ${provider.name}`"
          @click="$emit('edit-provider')"
        />
        <Button
          icon="pi pi-trash"
          text
          size="small"
          severity="danger"
          :aria-label="`Delete provider ${provider.name}`"
          @click="$emit('delete-provider')"
        />
      </div>
    </div>

    <ul v-if="models.length > 0">
      <li
        v-for="model in models"
        :key="model.id"
        class="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-surface px-3 py-2 sm:px-4"
      >
        <div class="min-w-0 flex-1">
          <div class="flex flex-wrap items-center gap-2">
            <span class="text-sm font-medium">{{ model.label }}</span>
            <Tag
              v-for="task in tasksFor(model)"
              :key="task"
              :value="task"
              severity="contrast"
              class="text-xs"
            />
          </div>
          <div class="mt-0.5 text-xs text-muted-color">
            <span class="font-mono">{{ model.modelName }}</span>
            <span class="mx-1.5" aria-hidden="true">·</span>
            <span class="whitespace-nowrap">{{ formatCosts(model) }}</span>
          </div>
        </div>
        <div class="flex shrink-0 items-center">
          <Button
            icon="pi pi-pencil"
            text
            size="small"
            severity="secondary"
            :aria-label="`Edit model ${model.label}`"
            @click="$emit('edit-model', model)"
          />
          <Button
            icon="pi pi-trash"
            text
            size="small"
            severity="danger"
            :aria-label="`Delete model ${model.label}`"
            @click="$emit('delete-model', model)"
          />
        </div>
      </li>
    </ul>
    <p v-else class="px-3 py-2 text-sm text-muted-color sm:px-4">No models yet.</p>

    <div class="p-2">
      <Button label="Add model" icon="pi pi-plus" text size="small" @click="$emit('add-model')" />
    </div>
  </div>
</template>

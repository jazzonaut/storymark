<script setup lang="ts">
import Button from "primevue/button";
import Textarea from "primevue/textarea";
import { useToast } from "primevue/usetoast";
import { computed, onMounted, ref } from "vue";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";

const { prompts, isLoading, loadPrompts, savePrompt } = useAdmin();
const { report } = useAdminError();
const toast = useToast();

const selectedKey = ref<string | null>(null);
const draft = ref("");

const promptList = computed(() =>
  prompts.value.map((prompt) => ({ key: prompt.key, label: prompt.key.replace(/^prompts\./, "") })),
);
const selected = computed(
  () => prompts.value.find((prompt) => prompt.key === selectedKey.value) ?? null,
);
const isDirty = computed(() => selected.value !== null && draft.value !== selected.value.text);

function select(key: string): void {
  selectedKey.value = key;
  draft.value = prompts.value.find((prompt) => prompt.key === key)?.text ?? "";
}

function revert(): void {
  if (selected.value !== null) {
    draft.value = selected.value.text;
  }
}

async function save(): Promise<void> {
  if (selectedKey.value === null) {
    return;
  }

  const key = selectedKey.value;
  try {
    await savePrompt(key, draft.value);
    draft.value = prompts.value.find((prompt) => prompt.key === key)?.text ?? draft.value;
    toast.add({ severity: "success", summary: "Saved", detail: key, life: 2500 });
  } catch (error) {
    report(error);
  }
}

onMounted(() => {
  void (async () => {
    try {
      await loadPrompts();
      const first = prompts.value[0];
      if (first !== undefined) {
        select(first.key);
      }
    } catch (error) {
      report(error);
    }
  })();
});
</script>

<template>
  <section>
    <h2 class="mb-4 text-lg font-semibold">Prompts</h2>

    <div class="flex gap-6">
      <ul class="flex w-56 shrink-0 flex-col gap-1">
        <li v-for="item in promptList" :key="item.key">
          <button
            type="button"
            class="w-full rounded px-3 py-2 text-left text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
            :class="{ 'bg-zinc-100 font-medium dark:bg-zinc-800': item.key === selectedKey }"
            @click="select(item.key)"
          >
            {{ item.label }}
          </button>
        </li>
      </ul>

      <div class="flex-1">
        <template v-if="selected !== null">
          <Textarea v-model="draft" class="w-full font-mono" rows="20" />
          <div class="mt-3 flex gap-2">
            <Button label="Save" :disabled="!isDirty" :loading="isLoading" @click="save" />
            <Button label="Revert" text :disabled="!isDirty" @click="revert" />
          </div>
        </template>
        <p v-else class="opacity-70">Select a prompt to edit.</p>
      </div>
    </div>
  </section>
</template>

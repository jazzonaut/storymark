<script setup lang="ts">
import Button from "primevue/button";
import Textarea from "primevue/textarea";
import { useToast } from "primevue/usetoast";
import { computed, onMounted, ref } from "vue";
import AdminPageHeader from "@/components/admin/AdminPageHeader.vue";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";

const { prompts, promptsLoading, loadPrompts, savePrompt } = useAdmin();
const { report, reportWithValidation } = useAdminError();
const toast = useToast();

const selectedKey = ref<string | null>(null);
const draft = ref("");
const saving = ref(false);

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
  if (selectedKey.value === null || saving.value) {
    return;
  }

  const key = selectedKey.value;
  saving.value = true;
  try {
    await savePrompt(key, draft.value);
    draft.value = prompts.value.find((prompt) => prompt.key === key)?.text ?? draft.value;
    toast.add({ severity: "success", summary: "Saved", detail: key, life: 2500 });
  } catch (error) {
    // The backend rejects templates missing required placeholders with a field
    // error; surface that text rather than a generic failure.
    reportWithValidation(error, "Couldn't save prompt");
  } finally {
    saving.value = false;
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
    <AdminPageHeader
      title="Prompts"
      description="Templates for every LLM task. Placeholders in braces are filled at call time; required ones can't be removed."
    />

    <p v-if="promptsLoading && prompts.length === 0" class="text-muted-color">Loading…</p>

    <div v-else class="flex flex-col gap-4 md:flex-row md:gap-6">
      <ul class="flex shrink-0 flex-col gap-1 md:w-56">
        <li v-for="item in promptList" :key="item.key">
          <button
            type="button"
            class="w-full rounded-border px-3 py-2 text-left text-sm hover:bg-emphasis"
            :class="{ 'bg-emphasis font-medium': item.key === selectedKey }"
            @click="select(item.key)"
          >
            {{ item.label }}
          </button>
        </li>
      </ul>

      <div class="min-w-0 flex-1">
        <template v-if="selected !== null">
          <Textarea v-model="draft" class="w-full font-mono text-sm" rows="20" />
          <div class="mt-3 flex gap-2">
            <Button label="Save" :disabled="!isDirty" :loading="saving" @click="save" />
            <Button label="Revert" text :disabled="!isDirty || saving" @click="revert" />
          </div>
        </template>
        <p v-else class="text-muted-color">Select a prompt to edit.</p>
      </div>
    </div>
  </section>
</template>

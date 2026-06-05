<script setup lang="ts">
import Button from "primevue/button";
import { useToast } from "primevue/usetoast";
import { computed, ref, watch } from "vue";
import type { EmbeddingImpactPreview } from "@/api/admin-types";
import AdminDialog from "@/components/admin/AdminDialog.vue";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";

const props = defineProps<{
  /** The candidate embedding model id, or null when closed. */
  modelId: string | null;
  /** Human-readable name for the candidate; falls back to the id when null. */
  modelLabel: string | null;
}>();

const emit = defineEmits<{
  (e: "applied"): void;
  /** Closed without applying — the caller reverts its selection. */
  (e: "close"): void;
}>();

const { previewEmbeddingSwap, applyEmbeddingSwap } = useAdmin();
const { report } = useAdminError();
const toast = useToast();

const preview = ref<EmbeddingImpactPreview | null>(null);
const loading = ref(false);
const applying = ref(false);

const visible = computed<boolean>(() => props.modelId !== null);
const displayName = computed<string>(() => props.modelLabel ?? props.modelId ?? "");

watch(
  () => props.modelId,
  async (modelId) => {
    if (modelId === null) {
      return;
    }
    preview.value = null;
    loading.value = true;
    try {
      preview.value = await previewEmbeddingSwap();
    } catch (error) {
      report(error);
      emit("close");
    } finally {
      loading.value = false;
    }
  },
);

function formatDuration(seconds: number): string {
  return seconds < 60 ? `~${String(seconds)}s` : `~${String(Math.round(seconds / 60))} min`;
}

async function confirm(): Promise<void> {
  const modelId = props.modelId;
  if (modelId === null) {
    return;
  }
  applying.value = true;
  try {
    const result = await applyEmbeddingSwap(modelId);
    toast.add({
      severity: "success",
      summary: "Reindex started",
      detail: `${String(result.booksQueued)} book(s) queued.`,
      life: 4000,
    });
    emit("applied");
  } catch (error) {
    report(error);
  } finally {
    applying.value = false;
  }
}
</script>

<template>
  <AdminDialog
    :visible="visible"
    header="Change embedding model"
    :busy="applying"
    wide
    @close="emit('close')"
  >
    <p class="mb-3">
      Switching to <strong>{{ displayName }}</strong> re-embeds every chunk under the new model.
      Search results for a book are incomplete until its reindex finishes.
    </p>

    <p v-if="loading" class="text-muted-color">Estimating impact…</p>
    <ul v-else-if="preview" class="mb-3 list-disc pl-5">
      <li>{{ preview.booksAffected }} book(s) will be reindexed</li>
      <li>{{ preview.chunkCount }} chunk(s) to re-embed</li>
      <li>Estimated time: {{ formatDuration(preview.estimatedSeconds) }}</li>
    </ul>

    <p class="text-sm text-muted-color">
      Track progress in the
      <a href="/hangfire" target="_blank" rel="noopener" class="underline">job dashboard</a>.
    </p>

    <template #footer>
      <Button label="Cancel" text :disabled="applying" @click="emit('close')" />
      <Button label="Reindex" :loading="applying" :disabled="loading" @click="confirm" />
    </template>
  </AdminDialog>
</template>

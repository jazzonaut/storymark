<script setup lang="ts">
import { computed, ref } from "vue";
import EntityTypeBadge from "@/components/reader/EntityTypeBadge.vue";
import CloseButton from "@/components/reader/CloseButton.vue";
import { useEntitySummary } from "@/composables/use-entity-summary";
import { useEntityMerge } from "@/composables/use-entity-merge";
import { useFocusTrap } from "@/composables/use-focus-trap";

const { openEntity, isLoading, summary, error, close } = useEntitySummary();
const { requestMerge } = useEntityMerge();

const panel = ref<HTMLElement | null>(null);
const isOpen = computed<boolean>(() => openEntity.value !== null);
useFocusTrap(isOpen, panel, close);

// Prefer the loaded canonical name; fall back to the tapped text while generating.
const heading = computed<string>(
  () => summary.value?.canonicalName ?? openEntity.value?.name ?? "",
);

// Hand off to the merge picker with this entity as the survivor (it keeps its name), then
// close the summary so the picker takes over.
function startMerge(): void {
  const entity = openEntity.value;
  if (entity === null) {
    return;
  }
  requestMerge({ entityId: entity.entityId, name: heading.value });
  close();
}
</script>

<template>
  <div v-if="openEntity" class="absolute inset-0 z-40">
    <div class="absolute inset-0 bg-black/30" @click="close"></div>
    <section
      ref="panel"
      role="dialog"
      aria-modal="true"
      aria-labelledby="entity-popup-heading"
      tabindex="-1"
      class="absolute inset-x-0 bottom-0 flex max-h-[80vh] flex-col rounded-t-2xl bg-[var(--sm-bg)] shadow-2xl outline-none sm:inset-x-auto sm:top-1/2 sm:left-1/2 sm:max-h-[70vh] sm:w-[28rem] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-2xl"
    >
      <header
        class="flex items-center justify-between gap-3 border-b border-[var(--sm-muted)]/15 px-5 py-3"
      >
        <div class="flex min-w-0 items-baseline gap-2">
          <h2
            id="entity-popup-heading"
            class="truncate text-base font-semibold text-[var(--sm-fg)]"
          >
            {{ heading }}
          </h2>
          <EntityTypeBadge v-if="summary" :type="summary.entityType" />
        </div>
        <div class="flex shrink-0 items-center gap-1">
          <button
            type="button"
            class="rounded-md px-2 py-1 text-xs font-medium text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
            title="Merge another entry into this one"
            @click="startMerge"
          >
            Merge…
          </button>
          <CloseButton @close="close" />
        </div>
      </header>

      <div class="min-h-0 flex-1 overflow-y-auto px-5 py-4">
        <div v-if="isLoading" class="space-y-2" aria-busy="true">
          <div class="h-3 w-2/3 animate-pulse rounded bg-[var(--sm-surface)]"></div>
          <div class="h-3 w-full animate-pulse rounded bg-[var(--sm-surface)]"></div>
          <div class="h-3 w-5/6 animate-pulse rounded bg-[var(--sm-surface)]"></div>
        </div>

        <p v-else-if="error" class="text-sm text-[var(--sm-muted)]">{{ error }}</p>

        <div v-else-if="summary" class="space-y-4">
          <p class="text-sm leading-relaxed text-[var(--sm-fg)]">{{ summary.shortSummary }}</p>

          <section v-if="summary.knownFacts.length > 0">
            <h3 class="mb-1 text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
              Known facts
            </h3>
            <ul class="list-disc space-y-1 pl-5 text-sm text-[var(--sm-fg)]">
              <li v-for="(fact, i) in summary.knownFacts" :key="i">{{ fact }}</li>
            </ul>
          </section>

          <section v-if="summary.relationships.length > 0">
            <h3 class="mb-1 text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
              Relationships
            </h3>
            <ul class="list-disc space-y-1 pl-5 text-sm text-[var(--sm-fg)]">
              <li v-for="(rel, i) in summary.relationships" :key="i">{{ rel }}</li>
            </ul>
          </section>

          <section v-if="summary.openQuestions.length > 0">
            <h3 class="mb-1 text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
              Open questions
            </h3>
            <ul class="list-disc space-y-1 pl-5 text-sm text-[var(--sm-fg)]">
              <li v-for="(q, i) in summary.openQuestions" :key="i">{{ q }}</li>
            </ul>
          </section>
        </div>
      </div>
    </section>
  </div>
</template>

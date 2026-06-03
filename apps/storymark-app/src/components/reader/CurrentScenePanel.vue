<script setup lang="ts">
import { ref, watch } from "vue";
import type { AssistantSource, CurrentScene, SectionSummary } from "@storymark/shared-types";
import MemoryEmptyState from "@/components/reader/MemoryEmptyState.vue";
import SourceList from "@/components/reader/SourceList.vue";
import { currentSceneApi } from "@/api/current-scene-api";
import { describeLoadError, useCachedLoad } from "@/composables/use-cached-load";

const props = defineProps<{
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  cacheVersion: string | null;
  manifest: readonly SectionSummary[];
  isProcessing: boolean;
}>();

const emit = defineEmits<{
  (e: "jump", source: AssistantSource): void;
  (e: "ask", question: string): void;
}>();

const scene = ref<CurrentScene | null>(null);
const loader = useCachedLoad();
const { isLoading, error } = loader;

async function load(): Promise<void> {
  if (props.currentSection === null) {
    scene.value = null;
    return;
  }
  const section = props.currentSection;

  await loader.run({
    load: async (ctx) => {
      const result = await currentSceneApi.get(
        props.bookId,
        section,
        props.currentLocation,
        props.cacheVersion,
        !props.isProcessing,
      );
      if (!ctx.isCurrent()) {
        return;
      }
      scene.value = result;
    },
    describeError: describeLoadError(
      "Couldn't load the current scene just now.",
      "The current scene needs a connection to generate.",
    ),
    onFailure: () => {
      scene.value = null;
    },
  });
}

// Each load is a fresh LLM generation, so reload on section change and on a reprocess
// (cacheVersion) — not on every in-section move, which would fire one generation per
// page turn. The card is built at whatever position the tab was opened at.
watch(
  () => [props.bookId, props.currentSection, props.cacheVersion] as const,
  () => {
    void load();
  },
  { immediate: true },
);
</script>

<template>
  <div class="flex h-full flex-col gap-4">
    <div v-if="isLoading" class="space-y-3">
      <div class="h-24 w-full animate-pulse rounded-lg bg-[var(--sm-surface)]"></div>
      <div class="h-20 w-full animate-pulse rounded-lg bg-[var(--sm-surface)]"></div>
    </div>

    <p v-else-if="error" class="text-sm text-[var(--sm-muted)]">{{ error }}</p>

    <MemoryEmptyState
      v-else-if="scene === null"
      :processing="isProcessing"
      idle="The current scene will appear once there is enough read context."
    />

    <template v-else>
      <section class="space-y-2">
        <h3 class="text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
          What's happening
        </h3>
        <p class="text-sm leading-relaxed whitespace-pre-line text-[var(--sm-fg)]">
          {{ scene.summary }}
        </p>
      </section>

      <section v-if="scene.presentEntities.length > 0" class="space-y-2">
        <h3 class="text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
          Present
        </h3>
        <ul class="space-y-2">
          <li
            v-for="entity in scene.presentEntities"
            :key="`${entity.name}:${entity.role}`"
            class="border-l-2 border-[var(--sm-accent)]/50 pl-3"
          >
            <p class="text-sm font-semibold text-[var(--sm-fg)]">{{ entity.name }}</p>
            <p class="text-sm leading-snug text-[var(--sm-muted)]">{{ entity.role }}</p>
          </li>
        </ul>
      </section>

      <section v-if="scene.place !== null" class="space-y-1">
        <h3 class="text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">Place</h3>
        <p class="text-sm leading-relaxed text-[var(--sm-fg)]">{{ scene.place }}</p>
      </section>

      <section v-if="scene.immediateTension !== null" class="space-y-1">
        <h3 class="text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
          Immediate tension
        </h3>
        <p class="text-sm leading-relaxed text-[var(--sm-fg)]">{{ scene.immediateTension }}</p>
      </section>

      <section v-if="scene.recentChange !== null" class="space-y-1">
        <h3 class="text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
          Recent change
        </h3>
        <p class="text-sm leading-relaxed text-[var(--sm-fg)]">{{ scene.recentChange }}</p>
      </section>

      <section v-if="scene.relevantThreads.length > 0" class="space-y-2">
        <h3 class="text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
          Relevant threads
        </h3>
        <ul class="list-disc space-y-1 pl-5 text-sm text-[var(--sm-fg)]">
          <li v-for="thread in scene.relevantThreads" :key="thread">{{ thread }}</li>
        </ul>
      </section>

      <section v-if="scene.suggestedQuestions.length > 0" class="space-y-2">
        <h3 class="text-xs font-semibold tracking-wide text-[var(--sm-muted)] uppercase">
          Need help?
        </h3>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="question in scene.suggestedQuestions"
            :key="question"
            type="button"
            class="rounded-md border border-[var(--sm-muted)]/20 bg-[var(--sm-surface)] px-3 py-1.5 text-left text-sm font-medium text-[var(--sm-fg)] hover:border-[var(--sm-accent)]"
            @click="emit('ask', question)"
          >
            {{ question }}
          </button>
        </div>
      </section>

      <SourceList
        :sources="scene.sources"
        :manifest="manifest"
        title="Recent passage"
        :open="false"
        @select="(source) => emit('jump', source)"
      />
    </template>
  </div>
</template>

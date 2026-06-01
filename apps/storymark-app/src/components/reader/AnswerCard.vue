<script setup lang="ts">
import { computed } from "vue";
import type { AssistantAnswer, AnswerConfidence, SectionSummary } from "@storymark/shared-types";
import SourceList from "@/components/reader/SourceList.vue";

const props = defineProps<{
  isAsking: boolean;
  answer: AssistantAnswer | null;
  error: string | null;
  question: string | null;
  manifest: readonly SectionSummary[];
}>();

defineEmits<{ (e: "select-source", source: AssistantAnswer["sources"][number]): void }>();

const confidenceClass = computed<string>(() => {
  const map: Record<AnswerConfidence, string> = {
    low: "bg-[var(--sm-surface)] text-[var(--sm-muted)]",
    medium: "bg-amber-500/15 text-amber-700",
    high: "bg-emerald-500/15 text-emerald-700",
  };
  return props.answer ? map[props.answer.confidence] : "";
});
</script>

<template>
  <div>
    <div v-if="isAsking" class="space-y-2" aria-busy="true">
      <div class="h-3 w-3/4 animate-pulse rounded bg-[var(--sm-surface)]"></div>
      <div class="h-3 w-full animate-pulse rounded bg-[var(--sm-surface)]"></div>
      <div class="h-3 w-5/6 animate-pulse rounded bg-[var(--sm-surface)]"></div>
    </div>

    <p v-else-if="error" class="text-sm text-red-600">{{ error }}</p>

    <div v-else-if="answer">
      <p v-if="question" class="mb-2 text-sm font-medium text-[var(--sm-fg)]">{{ question }}</p>
      <div class="mb-3 flex items-start gap-2">
        <span
          class="mt-0.5 shrink-0 rounded-full px-2 py-0.5 text-xs font-medium capitalize"
          :class="confidenceClass"
        >
          {{ answer.confidence }}
        </span>
        <p class="text-sm whitespace-pre-line text-[var(--sm-fg)]">{{ answer.answer }}</p>
      </div>
      <SourceList
        :sources="answer.sources"
        :manifest="manifest"
        @select="(source) => $emit('select-source', source)"
      />
    </div>

    <p v-else class="text-sm text-[var(--sm-muted)]">
      Ask anything about what you've read so far. Answers never reveal what's ahead.
    </p>
  </div>
</template>

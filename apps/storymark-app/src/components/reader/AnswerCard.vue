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

const emit = defineEmits<{
  (e: "select-source", source: AssistantAnswer["sources"][number]): void;
}>();

const confidenceClass = computed<string>(() => {
  const map: Record<AnswerConfidence, string> = {
    low: "bg-[var(--sm-surface)] text-[var(--sm-muted)]",
    medium: "bg-amber-500/15 text-amber-700",
    high: "bg-emerald-500/15 text-emerald-700",
  };
  return props.answer ? map[props.answer.confidence] : "";
});

type Segment = { readonly text: string } | { readonly cite: number };

// Split the answer on [n] markers so each marker can render as a chip that
// jumps to the passage it cites; plain text renders between them.
const segments = computed<readonly Segment[]>(() => {
  const text = props.answer?.answer ?? "";
  const parts: Segment[] = [];
  const marker = /\[(\d+)\]/g;
  let last = 0;
  let match: RegExpExecArray | null;
  while ((match = marker.exec(text)) !== null) {
    if (match.index > last) {
      parts.push({ text: text.slice(last, match.index) });
    }
    parts.push({ cite: Number(match[1]) });
    last = match.index + match[0].length;
  }
  if (last < text.length) {
    parts.push({ text: text.slice(last) });
  }
  return parts;
});

const hasCitations = computed<boolean>(() => segments.value.some((s) => "cite" in s));

function onCite(index: number): void {
  const source = props.answer?.sources.find((s) => s.index === index);
  if (source) {
    emit("select-source", source);
  }
}
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
        <p class="text-sm whitespace-pre-line text-[var(--sm-fg)]">
          <template v-for="(seg, i) in segments" :key="i">
            <button
              v-if="'cite' in seg"
              type="button"
              class="mx-0.5 align-super text-[0.65rem] font-semibold text-[var(--sm-accent)] hover:underline"
              @click="onCite(seg.cite)"
            >
              {{ seg.cite }}
            </button>
            <span v-else>{{ seg.text }}</span>
          </template>
        </p>
      </div>
      <SourceList
        :sources="answer.sources"
        :manifest="manifest"
        :title="hasCitations ? 'Sources' : 'Related passages'"
        :open="hasCitations"
        @select="(source) => emit('select-source', source)"
      />
    </div>

    <p v-else class="text-sm text-[var(--sm-muted)]">
      Ask anything about what you've read so far. Answers never reveal what's ahead.
    </p>
  </div>
</template>

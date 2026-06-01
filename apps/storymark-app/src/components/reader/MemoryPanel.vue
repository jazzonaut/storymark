<script setup lang="ts">
import { computed, ref, toRef } from "vue";
import type { AssistantSource, SectionSummary } from "@storymark/shared-types";
import QuestionInput from "@/components/reader/QuestionInput.vue";
import AnswerCard from "@/components/reader/AnswerCard.vue";
import SummaryPanel from "@/components/reader/SummaryPanel.vue";
import StoryGuidePanel from "@/components/reader/StoryGuidePanel.vue";
import TimelinePanel from "@/components/reader/TimelinePanel.vue";
import RelationshipGraphPanel from "@/components/reader/RelationshipGraphPanel.vue";
import { useAssistant } from "@/composables/use-assistant";
import { useFocusTrap } from "@/composables/use-focus-trap";
import { useMemoryPanel } from "@/composables/use-memory-panel";
import { useOfflineSync } from "@/composables/use-offline-sync";

const props = defineProps<{
  open: boolean;
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  manifest: readonly SectionSummary[];
  isProcessing: boolean;
}>();

const emit = defineEmits<{
  (e: "close"): void;
  (e: "jump", source: AssistantSource): void;
  (e: "jump-location", payload: { sectionIndex: number; locationStart: number }): void;
}>();

const { isAsking, lastQuestion, answer, error, ask } = useAssistant();
const { activeTab, tabs, setTab } = useMemoryPanel();
const { isOnline } = useOfflineSync();

const panel = ref<HTMLElement | null>(null);
useFocusTrap(toRef(props, "open"), panel);

// Q&A needs the network and a known position; section text stays readable offline.
const canAsk = computed<boolean>(() => isOnline.value && props.currentSection !== null);

function onAsk(question: string): void {
  if (props.currentSection === null) {
    return;
  }
  void ask(props.bookId, question, props.currentSection, props.currentLocation);
}
</script>

<template>
  <div v-if="open" class="absolute inset-0 z-30">
    <div class="absolute inset-0 bg-black/30" @click="emit('close')"></div>
    <section
      ref="panel"
      role="dialog"
      aria-modal="true"
      aria-label="Memory"
      tabindex="-1"
      class="absolute inset-x-0 bottom-0 flex max-h-[80vh] flex-col rounded-t-2xl bg-[var(--sm-bg)] shadow-2xl outline-none sm:inset-y-0 sm:right-0 sm:left-auto sm:max-h-none sm:w-[26rem] sm:rounded-none"
    >
      <div class="flex items-center justify-between border-b border-[var(--sm-muted)]/15 px-5 py-3">
        <nav class="flex flex-wrap gap-1">
          <button
            v-for="tab in tabs"
            :key="tab.id"
            type="button"
            class="rounded-md px-3 py-1 text-sm font-semibold"
            :class="
              activeTab === tab.id
                ? 'bg-[var(--sm-surface)] text-[var(--sm-fg)]'
                : 'text-[var(--sm-muted)] hover:text-[var(--sm-fg)]'
            "
            @click="setTab(tab.id)"
          >
            {{ tab.label }}
          </button>
        </nav>
        <button
          type="button"
          class="rounded px-2 py-1 text-sm text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
          @click="emit('close')"
        >
          Done
        </button>
      </div>

      <!-- Each non-assistant panel mounts only when its tab opens, so its data
           (and ECharts, for the timeline/graph) loads lazily. -->
      <div class="min-h-0 flex-1 overflow-y-auto px-5 py-4">
        <SummaryPanel
          v-if="activeTab === 'summary'"
          :book-id="bookId"
          :current-section="currentSection"
          :is-processing="isProcessing"
        />
        <TimelinePanel
          v-else-if="activeTab === 'timeline'"
          :book-id="bookId"
          :current-section="currentSection"
          :current-location="currentLocation"
          :manifest="manifest"
          :is-processing="isProcessing"
          @jump="(payload) => emit('jump-location', payload)"
        />
        <RelationshipGraphPanel
          v-else-if="activeTab === 'graph'"
          :book-id="bookId"
          :current-section="currentSection"
          :current-location="currentLocation"
          :is-processing="isProcessing"
        />
        <StoryGuidePanel
          v-else-if="activeTab === 'guide'"
          :book-id="bookId"
          :current-section="currentSection"
          :current-location="currentLocation"
          :manifest="manifest"
          :is-processing="isProcessing"
        />
        <AnswerCard
          v-else
          :is-asking="isAsking"
          :answer="answer"
          :error="error"
          :question="lastQuestion"
          :manifest="manifest"
          @select-source="(source) => emit('jump', source)"
        />
      </div>

      <div v-if="activeTab === 'assistant'" class="border-t border-[var(--sm-muted)]/15 px-5 py-3">
        <QuestionInput :disabled="!canAsk" :pending="isAsking" @ask="onAsk" />
      </div>
    </section>
  </div>
</template>

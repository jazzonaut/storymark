<script setup lang="ts">
import { computed, ref } from "vue";
import type { AssistantSource, SectionSummary } from "@storymark/shared-types";
import QuestionInput from "@/components/reader/QuestionInput.vue";
import AnswerCard from "@/components/reader/AnswerCard.vue";
import StoryGuidePanel from "@/components/reader/StoryGuidePanel.vue";
import { useAssistant } from "@/composables/use-assistant";
import { useOfflineSync } from "@/composables/use-offline-sync";

const props = defineProps<{
  open: boolean;
  bookId: string;
  currentSection: number | null;
  currentLocation: number;
  manifest: readonly SectionSummary[];
}>();

const emit = defineEmits<{
  (e: "close"): void;
  (e: "jump", source: AssistantSource): void;
}>();

type Tab = "assistant" | "guide";

const { isAsking, lastQuestion, answer, error, ask } = useAssistant();
const { isOnline } = useOfflineSync();

const activeTab = ref<Tab>("assistant");

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
  <div v-if="open" class="absolute inset-0 z-30" @click.self="emit('close')">
    <div class="absolute inset-0 bg-black/30"></div>
    <section
      class="absolute inset-x-0 bottom-0 flex max-h-[80vh] flex-col rounded-t-2xl bg-[var(--sm-bg)] shadow-2xl sm:inset-y-0 sm:right-0 sm:left-auto sm:max-h-none sm:w-[26rem] sm:rounded-none"
    >
      <div class="flex items-center justify-between border-b border-[var(--sm-muted)]/15 px-5 py-3">
        <!-- Timeline / Graph join here in S6.2/S6.3; S6.4 polishes all tabs. -->
        <nav class="flex gap-1">
          <button
            type="button"
            class="rounded-md px-3 py-1 text-sm font-semibold"
            :class="
              activeTab === 'assistant'
                ? 'bg-[var(--sm-surface)] text-[var(--sm-fg)]'
                : 'text-[var(--sm-muted)] hover:text-[var(--sm-fg)]'
            "
            @click="activeTab = 'assistant'"
          >
            Assistant
          </button>
          <button
            type="button"
            class="rounded-md px-3 py-1 text-sm font-semibold"
            :class="
              activeTab === 'guide'
                ? 'bg-[var(--sm-surface)] text-[var(--sm-fg)]'
                : 'text-[var(--sm-muted)] hover:text-[var(--sm-fg)]'
            "
            @click="activeTab = 'guide'"
          >
            Story Guide
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

      <div class="min-h-0 flex-1 overflow-y-auto px-5 py-4">
        <AnswerCard
          v-if="activeTab === 'assistant'"
          :is-asking="isAsking"
          :answer="answer"
          :error="error"
          :question="lastQuestion"
          :manifest="manifest"
          @select-source="(source) => emit('jump', source)"
        />
        <!-- Mounted only once the tab is opened, so its data loads lazily. -->
        <StoryGuidePanel
          v-else
          :book-id="bookId"
          :current-section="currentSection"
          :current-location="currentLocation"
        />
      </div>

      <div v-if="activeTab === 'assistant'" class="border-t border-[var(--sm-muted)]/15 px-5 py-3">
        <QuestionInput :disabled="!canAsk" :pending="isAsking" @ask="onAsk" />
      </div>
    </section>
  </div>
</template>

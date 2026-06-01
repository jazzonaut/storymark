import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";
import type { AssistantAnswer } from "@storymark/shared-types";
import { assistantApi } from "@/api/assistant-api";
import { ApiError } from "@/api/api-error";

// Module-level so the open panel keeps its last answer across reader interactions.
const isAsking = ref(false);
const lastQuestion = ref<string | null>(null);
const answer = shallowRef<AssistantAnswer | null>(null);
const error = ref<string | null>(null);
// The shared question-input model, so the selection popover can pre-fill it.
const draft = ref("");

interface UseAssistant {
  readonly isAsking: Ref<boolean>;
  readonly lastQuestion: Ref<string | null>;
  readonly answer: ShallowRef<AssistantAnswer | null>;
  readonly error: Ref<string | null>;
  readonly draft: Ref<string>;
  ask: (
    bookId: string,
    question: string,
    currentSection: number,
    currentLocation: number,
  ) => Promise<void>;
  reset: () => void;
}

export function useAssistant(): UseAssistant {
  async function ask(
    bookId: string,
    question: string,
    currentSection: number,
    currentLocation: number,
  ): Promise<void> {
    const trimmed = question.trim();
    if (trimmed.length === 0 || isAsking.value) {
      return;
    }

    isAsking.value = true;
    lastQuestion.value = trimmed;
    error.value = null;
    answer.value = null;
    try {
      answer.value = await assistantApi.ask(bookId, {
        question: trimmed,
        currentSection,
        currentLocation,
      });
    } catch (caught) {
      error.value =
        caught instanceof ApiError
          ? "Couldn't answer that just now. Try again in a moment."
          : "Q&A is unavailable offline.";
    } finally {
      isAsking.value = false;
    }
  }

  function reset(): void {
    isAsking.value = false;
    lastQuestion.value = null;
    answer.value = null;
    error.value = null;
    draft.value = "";
  }

  return { isAsking, lastQuestion, answer, error, draft, ask, reset };
}

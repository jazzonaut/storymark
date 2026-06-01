import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";
import type { AssistantAnswer } from "@storymark/shared-types";
import { assistantApi } from "@/api/assistant-api";
import { ApiError } from "@/api/api-error";

/** Where an answer was produced. An answer drawn from evidence up to this point can
 * reveal what's ahead of an earlier position, so the panel locks it until the reader
 * is back at or past this boundary in the same book (see {@link isWithinBoundary}). */
export interface AnswerBoundary {
  readonly bookId: string;
  readonly section: number;
  readonly location: number;
}

// Module-level so the open panel keeps its last answer across reader interactions.
const isAsking = ref(false);
const lastQuestion = ref<string | null>(null);
const answer = shallowRef<AssistantAnswer | null>(null);
// The reading position the current answer was produced at — its spoiler boundary.
const answerBoundary = shallowRef<AnswerBoundary | null>(null);
const error = ref<string | null>(null);
// The shared question-input model, so the selection popover can pre-fill it.
const draft = ref("");

/** True when an answer produced at `boundary` is safe to show at the reader's current
 * position: the same book, and a position at or beyond where the answer was produced. */
export function isWithinBoundary(
  boundary: AnswerBoundary,
  bookId: string,
  section: number,
  location: number,
): boolean {
  return (
    boundary.bookId === bookId &&
    (section > boundary.section || (section === boundary.section && location >= boundary.location))
  );
}

interface UseAssistant {
  readonly isAsking: Ref<boolean>;
  readonly lastQuestion: Ref<string | null>;
  readonly answer: ShallowRef<AssistantAnswer | null>;
  readonly answerBoundary: ShallowRef<AnswerBoundary | null>;
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
    answerBoundary.value = null;
    try {
      answer.value = await assistantApi.ask(bookId, {
        question: trimmed,
        currentSection,
        currentLocation,
      });
      answerBoundary.value = { bookId, section: currentSection, location: currentLocation };
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
    answerBoundary.value = null;
    error.value = null;
    draft.value = "";
  }

  return { isAsking, lastQuestion, answer, answerBoundary, error, draft, ask, reset };
}

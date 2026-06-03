import { computed, reactive, ref } from "vue";
import type { ComputedRef, Ref } from "vue";
import type { AssistantAnswer, AssistantHistoryTurn } from "@storymark/shared-types";
import type { AskQuestionBody } from "@/api/assistant-api";
import { assistantApi } from "@/api/assistant-api";
import { ApiError } from "@/api/api-error";
import { isTimeout } from "@/api/http";

// Follow-ups carry at most this many earlier turns; the server clamps again
// (and harder) on its side, this just keeps request payloads proportionate.
const MAX_HISTORY_TURNS = 6;

/** Where an answer was produced. An answer drawn from evidence up to this point can
 * reveal what's ahead of an earlier position, so the panel locks it until the reader
 * is back at or past this boundary in the same book (see {@link isWithinBoundary}). */
export interface AnswerBoundary {
  readonly bookId: string;
  readonly section: number;
  readonly location: number;
}

/** One Q&A turn of the assistant conversation. `boundary` is set when the answer
 * lands — until then there is nothing that could spoil. */
export interface AssistantTurn {
  readonly id: number;
  readonly question: string;
  answer: AssistantAnswer | null;
  boundary: AnswerBoundary | null;
  error: string | null;
}

/** The selected passage a conversation is anchored to. Its boundary is the
 * position it was selected at: behind that point the text itself may be ahead
 * of the reader, so it is neither shown nor sent. */
export interface SelectionContext {
  readonly text: string;
  readonly boundary: AnswerBoundary;
}

// Module-level so the open panel keeps the conversation across reader interactions.
const turns = ref<AssistantTurn[]>([]);
const selectionContext = ref<SelectionContext | null>(null);
// The question-input model, shared so anchoring a new conversation (or a reset)
// clears a typed-but-unsent question without remounting the input.
const draft = ref("");
let nextTurnId = 0;
// The in-flight request, so clearing the thread can abandon it instead of
// leaving an orphaned turn that would block the next ask until it settles.
let inFlight: AbortController | null = null;

// Derived rather than tracked: a turn is pending from push until its answer or
// error lands, and ask() refuses to start while one is pending, so "some turn is
// pending" is exactly "a request is in flight".
const isAsking = computed<boolean>(() =>
  turns.value.some((turn) => turn.answer === null && turn.error === null),
);

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
  readonly isAsking: ComputedRef<boolean>;
  readonly turns: Ref<AssistantTurn[]>;
  readonly selectionContext: Ref<SelectionContext | null>;
  readonly draft: Ref<string>;
  ask: (
    bookId: string,
    question: string,
    currentSection: number,
    currentLocation: number,
  ) => Promise<void>;
  startSelectionAsk: (text: string, boundary: AnswerBoundary) => void;
  clearSelection: () => void;
  newConversation: () => void;
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

    // Spoiler gate on the carried context: a turn answered (or a passage selected)
    // ahead of the current position must not feed this prompt — its content could
    // leak forward knowledge into an answer shown at an earlier point.
    const history: AssistantHistoryTurn[] = turns.value
      .filter(
        (turn): turn is AssistantTurn & { answer: AssistantAnswer; boundary: AnswerBoundary } =>
          turn.answer !== null &&
          turn.boundary !== null &&
          isWithinBoundary(turn.boundary, bookId, currentSection, currentLocation),
      )
      .slice(-MAX_HISTORY_TURNS)
      .map((turn) => ({ question: turn.question, answer: turn.answer.answer }));
    const selection = selectionContext.value;
    const selectedText =
      selection !== null &&
      isWithinBoundary(selection.boundary, bookId, currentSection, currentLocation)
        ? selection.text
        : null;

    const turn = reactive<AssistantTurn>({
      id: nextTurnId++,
      question: trimmed,
      answer: null,
      boundary: null,
      error: null,
    });
    turns.value.push(turn);

    const body: AskQuestionBody = {
      question: trimmed,
      ...(selectedText !== null ? { selectedText } : {}),
      ...(history.length > 0 ? { history } : {}),
      currentSection,
      currentLocation,
    };

    const controller = new AbortController();
    inFlight = controller;
    try {
      turn.answer = await assistantApi.ask(bookId, body, controller.signal);
      turn.boundary = { bookId, section: currentSection, location: currentLocation };
    } catch (caught) {
      // An abort means the thread was cleared mid-request: the turn is already
      // detached from `turns`, so there is nothing to report.
      if (!controller.signal.aborted) {
        turn.error =
          caught instanceof ApiError
            ? "Couldn't answer that just now. Try again in a moment."
            : isTimeout(caught)
              ? "The answer took too long. Try again."
              : "Q&A is unavailable offline.";
      }
    } finally {
      if (inFlight === controller) {
        inFlight = null;
      }
    }
  }

  // Clearing the thread abandons an in-flight ask: its turn could never be shown
  // again, and aborting also cancels the LLM call server-side.
  function clearThread(): void {
    inFlight?.abort();
    inFlight = null;
    turns.value = [];
    selectionContext.value = null;
  }

  /** "Ask…" from the selection popover: a fresh conversation anchored to the
   * selected passage. */
  function startSelectionAsk(text: string, boundary: AnswerBoundary): void {
    clearThread();
    selectionContext.value = { text, boundary };
    draft.value = "";
  }

  function clearSelection(): void {
    selectionContext.value = null;
  }

  function newConversation(): void {
    clearThread();
  }

  function reset(): void {
    clearThread();
    draft.value = "";
  }

  return {
    isAsking,
    turns,
    selectionContext,
    draft,
    ask,
    startSelectionAsk,
    clearSelection,
    newConversation,
    reset,
  };
}

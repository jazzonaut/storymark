import type { AssistantAnswer, AssistantHistoryTurn } from "@storymark/shared-types";
import { fetchJson, LLM_TIMEOUT_MS } from "@/api/http";

export interface AskQuestionBody {
  readonly question: string;
  /** The passage the conversation is anchored to, when asked from a selection. */
  readonly selectedText?: string;
  /** Earlier turns of the conversation — the client carries the thread; the
   * server is stateless and clamps/sanitises this before prompting. */
  readonly history?: readonly AssistantHistoryTurn[];
  readonly currentSection: number;
  readonly currentLocation: number;
}

export const assistantApi = {
  /** Asks a spoiler-safe question at a reading position. Throws
   * {@link ApiError} on failure (incl. offline, and 400 for a blank question).
   * `signal` lets the caller abandon the turn (clearing the thread mid-request);
   * aborting also cancels the LLM call server-side. */
  ask: (bookId: string, body: AskQuestionBody, signal: AbortSignal): Promise<AssistantAnswer> =>
    fetchJson<AssistantAnswer>(
      `/api/books/${encodeURIComponent(bookId)}/ask`,
      "Failed to ask question",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
        signal,
      },
      LLM_TIMEOUT_MS,
    ),
} as const;

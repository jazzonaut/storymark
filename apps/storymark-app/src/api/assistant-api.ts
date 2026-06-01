import type { AssistantAnswer } from "@storymark/shared-types";
import { fetchJson, LLM_TIMEOUT_MS } from "@/api/http";

export interface AskQuestionBody {
  readonly question: string;
  readonly currentSection: number;
  readonly currentLocation: number;
}

export const assistantApi = {
  /** Asks a spoiler-safe question at a reading position. Throws
   * {@link ApiError} on failure (incl. offline, and 400 for a blank question). */
  ask: (bookId: string, body: AskQuestionBody): Promise<AssistantAnswer> =>
    fetchJson<AssistantAnswer>(
      `/api/books/${encodeURIComponent(bookId)}/ask`,
      "Failed to ask question",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
      LLM_TIMEOUT_MS,
    ),
} as const;

import type { AssistantAnswer } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";

export interface AskQuestionBody {
  readonly question: string;
  readonly currentSection: number;
  readonly currentLocation: number;
}

export const assistantApi = {
  /** Asks a spoiler-safe question at a reading position. Throws
   * {@link ApiError} on failure (incl. offline, and 400 for a blank question). */
  ask: async (bookId: string, body: AskQuestionBody): Promise<AssistantAnswer> => {
    const response = await fetch(`/api/books/${encodeURIComponent(bookId)}/ask`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to ask question (${response.status}).`);
    }
    return (await response.json()) as AssistantAnswer;
  },
} as const;

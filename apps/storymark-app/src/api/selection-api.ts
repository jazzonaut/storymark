import type { SelectionHintResult } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";

export interface SelectionHintBody {
  readonly selectedText: string;
  readonly sectionIndex: number;
  readonly locationStart: number;
  readonly locationEnd: number;
  readonly currentLocation: number;
}

export const selectionApi = {
  /** Resolves a selection to an entity card or a 1B hint. Throws
   * {@link ApiError} on failure (incl. offline). */
  hint: async (bookId: string, body: SelectionHintBody): Promise<SelectionHintResult> => {
    const response = await fetch(`/api/books/${encodeURIComponent(bookId)}/selection/hint`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to resolve selection (${response.status}).`);
    }
    return (await response.json()) as SelectionHintResult;
  },
} as const;

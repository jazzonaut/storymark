import type { SelectionHintResult } from "@storymark/shared-types";
import { fetchJson, LLM_TIMEOUT_MS } from "@/api/http";

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
  hint: (bookId: string, body: SelectionHintBody): Promise<SelectionHintResult> =>
    fetchJson<SelectionHintResult>(
      `/api/books/${encodeURIComponent(bookId)}/selection/hint`,
      "Failed to resolve selection",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
      LLM_TIMEOUT_MS,
    ),
} as const;

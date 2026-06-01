import type { ReadingProgress } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { apiFetch, fetchJson } from "@/api/http";

export interface SaveProgressBody {
  readonly deviceId: string;
  readonly sectionIndex: number;
  readonly paragraphIndex: number;
  readonly locationStart: number;
  readonly locationEnd: number;
  readonly clientTimestamp: string;
}

export const progressApi = {
  /** The latest position across devices, or `null` if the book has none yet
   * (204). Throws {@link ApiError} on other failures (incl. offline). */
  get: async (bookId: string): Promise<ReadingProgress | null> => {
    const response = await apiFetch(`/api/books/${encodeURIComponent(bookId)}/progress`);
    if (response.status === 204) {
      return null;
    }
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to load progress (${response.status}).`);
    }
    return (await response.json()) as ReadingProgress;
  },

  /** Records a device's position and returns the canonical latest across devices. */
  save: (bookId: string, body: SaveProgressBody): Promise<ReadingProgress> =>
    fetchJson<ReadingProgress>(
      `/api/books/${encodeURIComponent(bookId)}/progress`,
      "Failed to save progress",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    ),
} as const;

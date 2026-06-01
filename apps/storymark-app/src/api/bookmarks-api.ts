import type { Bookmark } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { apiFetch, fetchJson } from "@/api/http";

export interface SaveBookmarkBody {
  readonly id: string;
  readonly sectionIndex: number;
  readonly paragraphIndex: number;
  readonly locationStart: number;
  readonly label: string | null;
  readonly createdAt: string;
}

export const bookmarksApi = {
  list: (bookId: string): Promise<readonly Bookmark[]> =>
    fetchJson<readonly Bookmark[]>(
      `/api/books/${encodeURIComponent(bookId)}/bookmarks`,
      "Failed to load bookmarks",
    ),

  save: (bookId: string, body: SaveBookmarkBody): Promise<Bookmark> =>
    fetchJson<Bookmark>(
      `/api/books/${encodeURIComponent(bookId)}/bookmarks`,
      "Failed to save bookmark",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    ),

  remove: async (bookId: string, bookmarkId: string): Promise<void> => {
    const response = await apiFetch(
      `/api/books/${encodeURIComponent(bookId)}/bookmarks/${encodeURIComponent(bookmarkId)}`,
      { method: "DELETE" },
    );
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to delete bookmark (${response.status}).`);
    }
  },
} as const;

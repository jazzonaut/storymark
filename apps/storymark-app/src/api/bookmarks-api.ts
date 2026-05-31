import type { Bookmark } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";

export interface SaveBookmarkBody {
  readonly id: string;
  readonly sectionIndex: number;
  readonly paragraphIndex: number;
  readonly locationStart: number;
  readonly label: string | null;
  readonly createdAt: string;
}

export const bookmarksApi = {
  list: async (bookId: string): Promise<readonly Bookmark[]> => {
    const response = await fetch(`/api/books/${encodeURIComponent(bookId)}/bookmarks`);
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to load bookmarks (${response.status}).`);
    }
    return (await response.json()) as readonly Bookmark[];
  },

  save: async (bookId: string, body: SaveBookmarkBody): Promise<Bookmark> => {
    const response = await fetch(`/api/books/${encodeURIComponent(bookId)}/bookmarks`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to save bookmark (${response.status}).`);
    }
    return (await response.json()) as Bookmark;
  },

  remove: async (bookId: string, bookmarkId: string): Promise<void> => {
    const response = await fetch(
      `/api/books/${encodeURIComponent(bookId)}/bookmarks/${encodeURIComponent(bookmarkId)}`,
      { method: "DELETE" },
    );
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to delete bookmark (${response.status}).`);
    }
  },
} as const;

import type { BookSummary } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { apiFetch, fetchJson } from "@/api/http";

// An upload streams a whole EPUB (up to the server's size cap) and the server
// hashes it before responding, so it needs far more headroom than a JSON call.
const UPLOAD_TIMEOUT_MS = 5 * 60_000;

export const booksApi = {
  list: (): Promise<readonly BookSummary[]> =>
    fetchJson<readonly BookSummary[]>("/api/books", "Failed to load books"),

  upload: async (file: File): Promise<BookSummary> => {
    const form = new FormData();
    form.append("file", file);

    const response = await apiFetch(
      "/api/books",
      { method: "POST", body: form },
      UPLOAD_TIMEOUT_MS,
    );
    if (!response.ok) {
      const body = (await response.json().catch(() => null)) as { message?: string } | null;
      throw new ApiError(response.status, body?.message ?? `Upload failed (${response.status}).`);
    }
    return (await response.json()) as BookSummary;
  },

  remove: async (bookId: string): Promise<void> => {
    const response = await apiFetch(`/api/books/${encodeURIComponent(bookId)}`, {
      method: "DELETE",
    });
    // 404 means it is already gone — the desired end state, so treat it as success.
    if (!response.ok && response.status !== 404) {
      throw new ApiError(response.status, `Failed to delete book (${response.status}).`);
    }
  },
} as const;

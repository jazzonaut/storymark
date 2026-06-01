import type { BookSummary } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { apiFetch, fetchJson } from "@/api/http";
import { db } from "@/db/storymark-db";

// An upload streams a whole EPUB (up to the server's size cap) and the server
// hashes it before responding, so it needs far more headroom than a JSON call.
const UPLOAD_TIMEOUT_MS = 5 * 60_000;

/** The library list and whether it came from the local cache (i.e. the API was
 * unreachable). `fromCache` drives the library's offline indicator off real API
 * reachability rather than `navigator.onLine`, which still reports "online" on a
 * network that simply can't route to the API (e.g. off the VPN). */
export interface LibraryListResult {
  readonly books: readonly BookSummary[];
  readonly fromCache: boolean;
}

export const booksApi = {
  // Network-first with a write-back cache: a fresh list refreshes the cache (so the
  // processing poll stays live), and an unreachable API falls back to the last-seen
  // list so the library survives offline. Throws only when nothing is cached yet.
  list: async (): Promise<LibraryListResult> => {
    try {
      const books = await fetchJson<readonly BookSummary[]>("/api/books", "Failed to load books");
      await db.library.put({ id: "all", books, cachedAt: Date.now() });
      return { books, fromCache: false };
    } catch (caught) {
      const cached = await db.library.get("all");
      if (cached) {
        return { books: cached.books, fromCache: true };
      }
      throw caught;
    }
  },

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

import type { BookSummary } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";

export const booksApi = {
  list: async (): Promise<readonly BookSummary[]> => {
    const response = await fetch("/api/books");
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to load books (${response.status}).`);
    }
    return (await response.json()) as readonly BookSummary[];
  },

  upload: async (file: File): Promise<BookSummary> => {
    const form = new FormData();
    form.append("file", file);

    const response = await fetch("/api/books", { method: "POST", body: form });
    if (!response.ok) {
      const body = (await response.json().catch(() => null)) as { message?: string } | null;
      throw new ApiError(response.status, body?.message ?? `Upload failed (${response.status}).`);
    }
    return (await response.json()) as BookSummary;
  },
} as const;

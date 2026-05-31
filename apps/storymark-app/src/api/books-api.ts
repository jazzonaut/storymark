import type { BookSummary } from "@storymark/shared-types";

/** Thrown on any non-2xx books response. */
export class BooksApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "BooksApiError";
    this.status = status;
  }
}

export const booksApi = {
  list: async (): Promise<readonly BookSummary[]> => {
    const response = await fetch("/api/books");
    if (!response.ok) {
      throw new BooksApiError(response.status, `Failed to load books (${response.status}).`);
    }
    return (await response.json()) as readonly BookSummary[];
  },

  upload: async (file: File): Promise<BookSummary> => {
    const form = new FormData();
    form.append("file", file);

    const response = await fetch("/api/books", { method: "POST", body: form });
    if (!response.ok) {
      const body = (await response.json().catch(() => null)) as { message?: string } | null;
      throw new BooksApiError(
        response.status,
        body?.message ?? `Upload failed (${response.status}).`,
      );
    }
    return (await response.json()) as BookSummary;
  },
} as const;

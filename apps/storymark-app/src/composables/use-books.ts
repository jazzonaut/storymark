import { shallowRef } from "vue";
import type { ShallowRef } from "vue";
import type { BookSummary, ProcessingStatus } from "@storymark/shared-types";
import { booksApi } from "@/api/books-api";
import { purgeBookData } from "@/db/storymark-db";

const TERMINAL: ReadonlySet<ProcessingStatus> = new Set<ProcessingStatus>(["Indexed", "Failed"]);

const books = shallowRef<readonly BookSummary[]>([]);
const isLoading = shallowRef(false);
let inFlight: Promise<void> | null = null;

interface UseBooks {
  readonly books: ShallowRef<readonly BookSummary[]>;
  readonly isLoading: ShallowRef<boolean>;
  loadBooks: () => Promise<void>;
  hasPending: () => boolean;
  upsert: (book: BookSummary) => void;
  remove: (bookId: string) => Promise<void>;
}

export function useBooks(): UseBooks {
  // Coalesce overlapping loads (the library poll can fire while a slow list()
  // is still running) so responses can't be applied out of order.
  async function fetchBooks(): Promise<void> {
    isLoading.value = true;
    try {
      books.value = await booksApi.list();
    } finally {
      isLoading.value = false;
      inFlight = null;
    }
  }

  function loadBooks(): Promise<void> {
    inFlight ??= fetchBooks();
    return inFlight;
  }

  // True while any book is still being processed; drives the library's status
  // poll. Nothing advances past Uploaded until the S2.1 pipeline exists.
  function hasPending(): boolean {
    return books.value.some((book) => !TERMINAL.has(book.processingStatus));
  }

  function upsert(book: BookSummary): void {
    const without = books.value.filter((existing) => existing.bookId !== book.bookId);
    books.value = [book, ...without];
  }

  // Wipe the book server-side, then drop its local caches and the in-memory list
  // entry. Server-first: if the request fails we keep the book rather than hiding
  // a book that still exists.
  async function remove(bookId: string): Promise<void> {
    await booksApi.remove(bookId);
    await purgeBookData(bookId);
    books.value = books.value.filter((existing) => existing.bookId !== bookId);
  }

  return { books, isLoading, loadBooks, hasPending, upsert, remove };
}

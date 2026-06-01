import { shallowRef } from "vue";
import type { ShallowRef } from "vue";
import type { BookSummary, ProcessingStatus } from "@storymark/shared-types";
import { booksApi } from "@/api/books-api";
import type { BookVersion } from "@/db/storymark-db";
import { purgeBookData } from "@/db/storymark-db";

/** How a book's offline content should be cache-gated: its extraction `version`
 * (`updatedAt`, null until the list is known) and whether its derived data is complete
 * enough to write back (`canCache` is false only while we positively know the book is
 * mid-processing). Shared by the section cache and the memory surfaces so a reprocess
 * invalidates them and an incomplete mid-processing payload is never persisted. */
export interface BookCacheParams {
  readonly version: BookVersion | null;
  readonly canCache: boolean;
}

const TERMINAL: ReadonlySet<ProcessingStatus> = new Set<ProcessingStatus>(["Indexed", "Failed"]);

const books = shallowRef<readonly BookSummary[]>([]);
const isLoading = shallowRef(false);
// True when the last list load couldn't reach the API (served cache, or nothing
// cached). Reflects real API reachability, unlike navigator.onLine.
const offline = shallowRef(false);
let inFlight: Promise<void> | null = null;

interface UseBooks {
  readonly books: ShallowRef<readonly BookSummary[]>;
  readonly isLoading: ShallowRef<boolean>;
  readonly offline: ShallowRef<boolean>;
  loadBooks: () => Promise<void>;
  hasPending: () => boolean;
  cacheParams: (bookId: string) => BookCacheParams;
  upsert: (book: BookSummary) => void;
  remove: (bookId: string) => Promise<void>;
}

export function useBooks(): UseBooks {
  // Coalesce overlapping loads (the library poll can fire while a slow list()
  // is still running) so responses can't be applied out of order.
  async function fetchBooks(): Promise<void> {
    isLoading.value = true;
    try {
      const result = await booksApi.list();
      books.value = result.books;
      offline.value = result.fromCache;
    } catch (caught) {
      // Couldn't reach the API and nothing was cached: keep whatever we had and
      // flag offline so the view explains the empty list instead of claiming it.
      offline.value = true;
      throw caught;
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

  // An unknown book (list not loaded yet — e.g. an offline deep-link) is treated as
  // cacheable with no version gate, matching the reader's memory-surface handling: the
  // gate only kicks in once we know the book is mid-processing or know its version.
  function cacheParams(bookId: string): BookCacheParams {
    const book = books.value.find((existing) => existing.bookId === bookId);
    return {
      version: book?.updatedAt ?? null,
      canCache: book === undefined || TERMINAL.has(book.processingStatus),
    };
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

  return { books, isLoading, offline, loadBooks, hasPending, cacheParams, upsert, remove };
}

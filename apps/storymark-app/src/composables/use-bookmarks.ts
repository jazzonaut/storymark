import { ref, shallowRef, watch } from "vue";
import type { Ref, ShallowRef } from "vue";
import { bookmarksApi } from "@/api/bookmarks-api";
import type { SaveBookmarkBody } from "@/api/bookmarks-api";
import { useOfflineSync } from "@/composables/use-offline-sync";
import { db } from "@/db/storymark-db";
import type { StoredBookmark } from "@/db/storymark-db";

export interface BookmarkPosition {
  readonly sectionIndex: number;
  readonly paragraphIndex: number;
  readonly locationStart: number;
}

const currentBookId = ref<string | null>(null);
const bookmarks = shallowRef<readonly StoredBookmark[]>([]);

function toBody(row: StoredBookmark): SaveBookmarkBody {
  return {
    id: row.id,
    sectionIndex: row.sectionIndex,
    paragraphIndex: row.paragraphIndex,
    locationStart: row.locationStart,
    label: row.label,
    createdAt: row.createdAt,
  };
}

// Push one pending row: a tombstone is DELETEd then dropped; a create is POSTed
// then marked synced — unless the user toggled it off mid-request.
async function syncOne(row: StoredBookmark): Promise<void> {
  if (row.deleted) {
    try {
      await bookmarksApi.remove(row.bookId, row.id);
      await db.bookmarks.delete(row.id);
    } catch {
      // stays queued; retried on next flush / reconnect
    }
    return;
  }
  try {
    await bookmarksApi.save(row.bookId, toBody(row));
  } catch {
    return;
  }
  const current = await db.bookmarks.get(row.id);
  if (current && !current.deleted && !current.synced) {
    await db.bookmarks.put({ ...current, synced: true });
  }
}

async function doFlush(): Promise<void> {
  const rows = await db.bookmarks.toArray();
  await Promise.all(rows.filter((row) => !row.synced).map((row) => syncOne(row)));
}

// Serialize flushes so a create's POST and its later tombstone's DELETE can't
// race at the server (which would leave an orphaned, resurrectable bookmark).
let flushChain: Promise<void> = Promise.resolve();
function flush(): Promise<void> {
  flushChain = flushChain.catch(() => undefined).then(() => doFlush());
  return flushChain;
}

async function refresh(bookId: string): Promise<void> {
  const rows = await db.bookmarks.where("bookId").equals(bookId).toArray();
  bookmarks.value = rows
    .filter((row) => !row.deleted)
    .toSorted((a, b) => a.sectionIndex - b.sectionIndex || a.locationStart - b.locationStart);
}

// Push pending ops, then pull the server's set as the truth for already-synced
// rows (keeping any still-pending local creates/tombstones). Offline: keep local.
async function load(bookId: string): Promise<void> {
  if (currentBookId.value !== bookId) {
    bookmarks.value = [];
  }
  currentBookId.value = bookId;
  await flush();
  try {
    const server = await bookmarksApi.list(bookId);
    const local = await db.bookmarks.where("bookId").equals(bookId).toArray();
    const pending = new Set(local.filter((row) => !row.synced).map((row) => row.id));
    await db.bookmarks.bulkDelete(local.filter((row) => row.synced).map((row) => row.id));
    await db.bookmarks.bulkPut(
      server
        .filter((s) => !pending.has(s.id))
        .map((s) => ({
          id: s.id,
          bookId,
          sectionIndex: s.sectionIndex,
          paragraphIndex: s.paragraphIndex ?? 0,
          locationStart: s.locationStart,
          label: s.label,
          createdAt: s.createdAt,
          synced: true,
          deleted: false,
        })),
    );
  } catch {
    // offline / unreachable: the local set stands
  }
  await refresh(bookId);
}

async function removeLocal(row: StoredBookmark): Promise<void> {
  // Always tombstone — even an unsynced create may be mid-POST, so we must DELETE
  // it on the server (idempotent) rather than just dropping it locally, else a
  // toggle-on-then-off race would leave an orphaned server bookmark. syncOne
  // hard-deletes the row once the server DELETE succeeds.
  await db.bookmarks.put({ ...row, deleted: true, synced: false });
}

// Flush pending bookmarks when connectivity returns, then re-pull the open book.
const { isOnline } = useOfflineSync();
watch(isOnline, (online) => {
  if (online && currentBookId.value !== null) {
    void load(currentBookId.value);
  }
});

interface UseBookmarks {
  readonly bookmarks: ShallowRef<readonly StoredBookmark[]>;
  readonly currentBookId: Ref<string | null>;
  load: (bookId: string) => Promise<void>;
  toggle: (bookId: string, position: BookmarkPosition, label: string | null) => Promise<void>;
  removeById: (id: string) => Promise<void>;
  isBookmarked: (sectionIndex: number, paragraphIndex: number) => boolean;
}

export function useBookmarks(): UseBookmarks {
  async function toggle(
    bookId: string,
    position: BookmarkPosition,
    label: string | null,
  ): Promise<void> {
    const match = bookmarks.value.find(
      (b) =>
        b.sectionIndex === position.sectionIndex && b.paragraphIndex === position.paragraphIndex,
    );
    if (match) {
      await removeLocal(match);
    } else {
      await db.bookmarks.put({
        id: crypto.randomUUID(),
        bookId,
        sectionIndex: position.sectionIndex,
        paragraphIndex: position.paragraphIndex,
        locationStart: position.locationStart,
        label,
        createdAt: new Date().toISOString(),
        synced: false,
        deleted: false,
      });
    }
    await refresh(bookId);
    void flush();
  }

  async function removeById(id: string): Promise<void> {
    const row = await db.bookmarks.get(id);
    if (!row) {
      return;
    }
    await removeLocal(row);
    if (currentBookId.value !== null) {
      await refresh(currentBookId.value);
    }
    void flush();
  }

  function isBookmarked(sectionIndex: number, paragraphIndex: number): boolean {
    return bookmarks.value.some(
      (b) => b.sectionIndex === sectionIndex && b.paragraphIndex === paragraphIndex,
    );
  }

  return { bookmarks, currentBookId, load, toggle, removeById, isBookmarked };
}

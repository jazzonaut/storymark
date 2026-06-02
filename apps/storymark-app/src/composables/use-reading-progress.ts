import { progressApi } from "@/api/progress-api";
import type { SaveProgressBody } from "@/api/progress-api";
import { useOfflineSync } from "@/composables/use-offline-sync";
import { db } from "@/db/storymark-db";
import type { StoredProgress } from "@/db/storymark-db";
import { getDeviceId } from "@/utils/device-id";

const SYNC_THROTTLE_MS = 5000;

export interface ReadingPosition {
  readonly sectionIndex: number;
  readonly paragraphIndex: number;
  readonly pageIndex?: number;
  readonly locationStart: number;
  readonly locationEnd: number;
}

const { registerSyncTask, requestFlush } = useOfflineSync();

let syncTimer: number | undefined;

function toBody(local: StoredProgress): SaveProgressBody {
  return {
    deviceId: getDeviceId(),
    sectionIndex: local.sectionIndex,
    paragraphIndex: local.paragraphIndex,
    locationStart: local.locationStart,
    locationEnd: local.locationEnd,
    clientTimestamp: local.clientTimestamp,
  };
}

// Push a book's local position to the server, then mark it synced — but only if
// the user hasn't advanced since (else the newer row stays pending). Returns false
// only on a failed push, so the caller arms a retry for that but not for a row left
// pending because the user advanced mid-flush (the next throttled sync handles it).
async function syncBook(bookId: string): Promise<boolean> {
  const local = await db.progress.get(bookId);
  if (!local || local.synced) {
    return true;
  }
  try {
    await progressApi.save(bookId, toBody(local));
  } catch {
    return false;
  }
  const current = await db.progress.get(bookId);
  if (current && current.clientTimestamp === local.clientTimestamp) {
    await db.progress.put({ ...current, synced: true });
  }
  return true;
}

// Push every book's pending position and report whether any push failed, so the
// shared backoff retries only genuine failures (not a position still advancing).
async function flushPending(): Promise<boolean> {
  const rows = await db.progress.toArray();
  const results = await Promise.all(
    rows.filter((row) => !row.synced).map((row) => syncBook(row.bookId)),
  );
  return results.some((ok) => !ok);
}

// Retry queued writes through the shared reachability/backoff scheduler, and flush
// once at startup to drain anything left pending by a previous session.
registerSyncTask(flushPending);
requestFlush();

// Trailing throttle: at most one flush kick per SYNC_THROTTLE_MS while reading.
function scheduleSync(): void {
  if (syncTimer === undefined) {
    syncTimer = window.setTimeout(() => {
      syncTimer = undefined;
      requestFlush();
    }, SYNC_THROTTLE_MS);
  }
}

function positionFromLocal(local: StoredProgress): ReadingPosition {
  return {
    sectionIndex: local.sectionIndex,
    paragraphIndex: local.paragraphIndex,
    ...(local.pageIndex === undefined ? {} : { pageIndex: local.pageIndex }),
    locationStart: local.locationStart,
    locationEnd: local.locationEnd,
  };
}

// Mirror the server's canonical position locally (as synced) and return it. The
// page index is dropped: it's a device/layout-local refinement that doesn't apply
// to a position resolved from the server.
async function adoptServer(
  bookId: string,
  server: NonNullable<Awaited<ReturnType<typeof progressApi.get>>>,
): Promise<ReadingPosition> {
  const paragraphIndex = server.paragraphIndex ?? 0;
  await db.progress.put({
    bookId,
    sectionIndex: server.sectionIndex,
    paragraphIndex,
    locationStart: server.locationStart,
    locationEnd: server.locationEnd,
    clientTimestamp: server.clientTimestamp,
    synced: true,
  });
  return {
    sectionIndex: server.sectionIndex,
    paragraphIndex,
    locationStart: server.locationStart,
    locationEnd: server.locationEnd,
  };
}

interface UseReadingProgress {
  record: (bookId: string, position: ReadingPosition) => Promise<void>;
  resume: (bookId: string) => Promise<ReadingPosition | null>;
  flush: () => Promise<void>;
}

export function useReadingProgress(): UseReadingProgress {
  // Persist locally immediately (so an offline reopen resumes), then throttle the
  // server push.
  async function record(bookId: string, position: ReadingPosition): Promise<void> {
    const row: StoredProgress = {
      bookId,
      sectionIndex: position.sectionIndex,
      paragraphIndex: position.paragraphIndex,
      ...(position.pageIndex === undefined ? {} : { pageIndex: position.pageIndex }),
      locationStart: position.locationStart,
      locationEnd: position.locationEnd,
      clientTimestamp: new Date().toISOString(),
      synced: false,
    };
    await db.progress.put(row);
    scheduleSync();
  }

  // Resolve where to open. Ordering is delegated entirely to the server's
  // receive-time ordering (the backend's ReadingProgressService.GetLatestAsync) —
  // the one ordering contract both sides share — so client wall-clocks are never
  // compared across devices here; a skewed clock would otherwise pin the book to a
  // stale position with a future client timestamp. See docs/adr/0020.
  //
  // A pending local write is this device's most recent action and hasn't reached
  // the server yet, so it wins and is flushed in the background. A synced mirror can
  // only be at or behind the server's canonical latest, so the server wins whenever
  // it's reachable, with the mirror as the offline fallback. Accepted trade-off
  // (ADR 0020): a pending write flushed late can become canonical over a position
  // another device synced further ahead in the meantime — last-flush-wins.
  async function resume(bookId: string): Promise<ReadingPosition | null> {
    const local = await db.progress.get(bookId);

    if (local && !local.synced) {
      requestFlush();
      return positionFromLocal(local);
    }

    let server: Awaited<ReturnType<typeof progressApi.get>> = null;
    try {
      server = await progressApi.get(bookId);
    } catch {
      server = null;
    }
    if (server) {
      return adoptServer(bookId, server);
    }
    return local ? positionFromLocal(local) : null;
  }

  async function flush(): Promise<void> {
    await flushPending();
  }

  return { record, resume, flush };
}

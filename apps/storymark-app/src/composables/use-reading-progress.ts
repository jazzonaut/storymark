import { watch } from "vue";
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
  readonly locationStart: number;
  readonly locationEnd: number;
}

let syncTimer: number | undefined;
let pendingSyncBookId: string | null = null;

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
// the user hasn't advanced since (else the newer row stays pending).
async function syncBook(bookId: string): Promise<void> {
  const local = await db.progress.get(bookId);
  if (!local || local.synced) {
    return;
  }
  try {
    await progressApi.save(bookId, toBody(local));
  } catch {
    return;
  }
  const current = await db.progress.get(bookId);
  if (current && current.clientTimestamp === local.clientTimestamp) {
    await db.progress.put({ ...current, synced: true });
  }
}

// Trailing throttle: at most one POST per SYNC_THROTTLE_MS while reading.
function scheduleSync(bookId: string): void {
  pendingSyncBookId = bookId;
  if (syncTimer === undefined) {
    syncTimer = window.setTimeout(() => {
      syncTimer = undefined;
      const target = pendingSyncBookId;
      pendingSyncBookId = null;
      if (target !== null) {
        void syncBook(target);
      }
    }, SYNC_THROTTLE_MS);
  }
}

async function flush(): Promise<void> {
  const rows = await db.progress.toArray();
  await Promise.all(rows.filter((row) => !row.synced).map((row) => syncBook(row.bookId)));
}

// Flush queued writes when connectivity returns.
const { isOnline } = useOfflineSync();
watch(isOnline, (online) => {
  if (online) {
    void flush();
  }
});

interface UseReadingProgress {
  record: (bookId: string, position: ReadingPosition) => Promise<void>;
  resume: (bookId: string) => Promise<ReadingPosition | null>;
  flush: () => Promise<void>;
}

export function useReadingProgress(): UseReadingProgress {
  // Persist locally immediately (so an offline reopen resumes), then throttle the
  // server push.
  async function record(bookId: string, position: ReadingPosition): Promise<void> {
    await db.progress.put({
      bookId,
      sectionIndex: position.sectionIndex,
      paragraphIndex: position.paragraphIndex,
      locationStart: position.locationStart,
      locationEnd: position.locationEnd,
      clientTimestamp: new Date().toISOString(),
      synced: false,
    });
    scheduleSync(bookId);
  }

  // Resolve the position to open at: the newer of the local mirror and the
  // server's latest-across-devices (last-write-wins by clientTimestamp). Network
  // failure falls back to local; a newer server position is mirrored locally.
  async function resume(bookId: string): Promise<ReadingPosition | null> {
    const local = await db.progress.get(bookId);
    let server: Awaited<ReturnType<typeof progressApi.get>> = null;
    try {
      server = await progressApi.get(bookId);
    } catch {
      server = null;
    }

    const localTs = local ? Date.parse(local.clientTimestamp) : Number.NEGATIVE_INFINITY;
    const serverTs = server ? Date.parse(server.clientTimestamp) : Number.NEGATIVE_INFINITY;

    if (local && !local.synced && localTs >= serverTs) {
      void syncBook(bookId);
    }
    if (server && serverTs > localTs) {
      await db.progress.put({
        bookId,
        sectionIndex: server.sectionIndex,
        paragraphIndex: server.paragraphIndex ?? 0,
        locationStart: server.locationStart,
        locationEnd: server.locationEnd,
        clientTimestamp: server.clientTimestamp,
        synced: true,
      });
    }

    const chosen = serverTs > localTs ? server : local;
    if (!chosen) {
      return null;
    }
    return {
      sectionIndex: chosen.sectionIndex,
      paragraphIndex: chosen.paragraphIndex ?? 0,
      locationStart: chosen.locationStart,
      locationEnd: chosen.locationEnd,
    };
  }

  return { record, resume, flush };
}

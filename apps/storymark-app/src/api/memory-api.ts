import type { MemorySnapshot } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { apiFetch } from "@/api/http";
import type { CachedResult } from "@/api/read-through-cache";
import { readThroughCache, versionTag } from "@/api/read-through-cache";
import type { BookVersion } from "@/db/storymark-db";
import { db } from "@/db/storymark-db";

async function fetchMemory(
  bookId: string,
  upToSection: number,
  upToLocation: number,
): Promise<MemorySnapshot | null> {
  const response = await apiFetch(
    `/api/books/${encodeURIComponent(bookId)}/memory?upToSection=${String(upToSection)}&upToLocation=${String(upToLocation)}`,
  );
  if (response.status === 204) {
    return null;
  }
  if (!response.ok) {
    throw new ApiError(response.status, `Failed to load memory (${response.status}).`);
  }
  return (await response.json()) as MemorySnapshot;
}

export const memoryApi = {
  // The section-entry recap: querying at (enteringSection, 0) returns the latest
  // snapshot strictly before this section — what the reader has finished. Read-through
  // (Dexie-first, write-back) like reader-api, so a recap seen online renders offline.
  // A 204 (no snapshot yet) is not cached — it may appear once processing catches up.
  //
  // The snapshot is stable for a given (book version, section entry), so a cached row
  // whose `version` still matches is served without a refetch. A reprocess bumps the
  // version, so a stale-versioned row is refetched (and only falls back to the old row,
  // flagged via `fallback`, when that refetch fails). `canCache` is false while the
  // book is still processing.
  getForSectionEntry: (
    bookId: string,
    enteringSection: number,
    version: BookVersion | null,
    canCache: boolean,
    onRefreshingFrom?: (snapshot: MemorySnapshot | null) => void,
  ): Promise<CachedResult<MemorySnapshot | null>> =>
    // No boundary and no `cachedAt`: the snapshot is location-independent and stable
    // for a given version, so the cache is version-gated rather than TTL-gated.
    readThroughCache<MemorySnapshot | null>({
      version,
      canCache,
      onRefreshingFrom,
      readCached: async () => {
        const row = await db.memorySnapshots.get([bookId, enteringSection]);
        return row ? { value: row.snapshot, ...versionTag(row.version) } : undefined;
      },
      fetchFresh: () => fetchMemory(bookId, enteringSection, 0),
      // A 204 (no snapshot yet) returns null — left uncached so it can still appear
      // once processing catches up.
      writeBack: (snapshot) =>
        snapshot === null
          ? Promise.resolve()
          : db.memorySnapshots.put({
              bookId,
              enteringSection,
              snapshot,
              ...versionTag(version),
            }),
    }),

  // The "Previously…" recap: the latest snapshot at-or-before the reader's exact
  // resumed position. Not cached (it fires once per open and the location key would
  // be unbounded), so it's a no-op offline — reading is unaffected.
  getAtPosition: (
    bookId: string,
    section: number,
    location: number,
  ): Promise<MemorySnapshot | null> => fetchMemory(bookId, section, location),
} as const;

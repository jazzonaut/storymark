import type { MemorySnapshot } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { db } from "@/db/storymark-db";

async function fetchForSectionEntry(
  bookId: string,
  enteringSection: number,
): Promise<MemorySnapshot | null> {
  // Querying at (enteringSection, 0) returns the latest snapshot strictly before
  // this section — i.e. the recap of everything the reader has finished.
  const response = await fetch(
    `/api/books/${encodeURIComponent(bookId)}/memory?upToSection=${String(enteringSection)}&upToLocation=0`,
  );
  if (response.status === 204) {
    return null;
  }
  if (!response.ok) {
    throw new ApiError(response.status, `Failed to load memory (${response.status}).`);
  }
  return (await response.json()) as MemorySnapshot;
}

// Read-through (Dexie-first, then API, write-back) like reader-api, so a recap seen
// online renders offline next time. A 204 (no snapshot yet) is not cached — the
// snapshot may appear once background processing catches up.
export const memoryApi = {
  getForSectionEntry: async (
    bookId: string,
    enteringSection: number,
  ): Promise<MemorySnapshot | null> => {
    const cached = await db.memorySnapshots.get([bookId, enteringSection]);
    if (cached) {
      return cached.snapshot;
    }
    const snapshot = await fetchForSectionEntry(bookId, enteringSection);
    if (snapshot) {
      await db.memorySnapshots.put({ bookId, enteringSection, snapshot });
    }
    return snapshot;
  },
} as const;

import type { MemorySnapshot } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { db } from "@/db/storymark-db";

async function fetchMemory(
  bookId: string,
  upToSection: number,
  upToLocation: number,
): Promise<MemorySnapshot | null> {
  const response = await fetch(
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
  getForSectionEntry: async (
    bookId: string,
    enteringSection: number,
  ): Promise<MemorySnapshot | null> => {
    const cached = await db.memorySnapshots.get([bookId, enteringSection]);
    if (cached) {
      return cached.snapshot;
    }
    const snapshot = await fetchMemory(bookId, enteringSection, 0);
    if (snapshot) {
      await db.memorySnapshots.put({ bookId, enteringSection, snapshot });
    }
    return snapshot;
  },

  // The "Previously…" recap: the latest snapshot at-or-before the reader's exact
  // resumed position. Not cached (it fires once per open and the location key would
  // be unbounded), so it's a no-op offline — reading is unaffected.
  getAtPosition: (
    bookId: string,
    section: number,
    location: number,
  ): Promise<MemorySnapshot | null> => fetchMemory(bookId, section, location),
} as const;

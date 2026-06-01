import type { Relationship } from "@storymark/shared-types";
import { fetchJson } from "@/api/http";
import { readThroughCache } from "@/api/read-through-cache";
import { db } from "@/db/storymark-db";

export const relationshipsApi = {
  /** Relationships visible at the reading position, for the graph. Read-through Dexie
   * cache (see {@link readThroughCache}): a stale row is still returned on a network
   * failure so the graph works offline. Throws {@link ApiError} only when nothing is
   * cached. */
  list: (
    bookId: string,
    upToSection: number,
    upToLocation: number,
  ): Promise<readonly Relationship[]> =>
    readThroughCache({
      readCached: async () => {
        const row = await db.graphs.get([bookId, upToSection]);
        return row ? { value: row.relationships, cachedAt: row.cachedAt } : undefined;
      },
      fetchFresh: () =>
        fetchJson<readonly Relationship[]>(
          `/api/books/${encodeURIComponent(bookId)}/relationships?upToSection=${String(upToSection)}&upToLocation=${String(upToLocation)}`,
          "Failed to load relationships",
        ),
      writeBack: (relationships) =>
        db.graphs.put({ bookId, upToSection, relationships, cachedAt: Date.now() }),
    }),
} as const;

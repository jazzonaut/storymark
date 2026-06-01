import type { BookEvent } from "@storymark/shared-types";
import { fetchJson } from "@/api/http";
import { readThroughCache } from "@/api/read-through-cache";
import { db } from "@/db/storymark-db";

export const eventsApi = {
  /** Events visible at the reading position, for the timeline. Read-through Dexie
   * cache (see {@link readThroughCache}): a stale row is still returned on a network
   * failure so the timeline works offline. Throws {@link ApiError} only when nothing
   * is cached. */
  list: (
    bookId: string,
    upToSection: number,
    upToLocation: number,
  ): Promise<readonly BookEvent[]> =>
    readThroughCache({
      readCached: async () => {
        const row = await db.timelines.get([bookId, upToSection]);
        return row ? { value: row.events, cachedAt: row.cachedAt } : undefined;
      },
      fetchFresh: () =>
        fetchJson<readonly BookEvent[]>(
          `/api/books/${encodeURIComponent(bookId)}/events?upToSection=${String(upToSection)}&upToLocation=${String(upToLocation)}`,
          "Failed to load events",
        ),
      writeBack: (events) =>
        db.timelines.put({ bookId, upToSection, events, cachedAt: Date.now() }),
    }),
} as const;

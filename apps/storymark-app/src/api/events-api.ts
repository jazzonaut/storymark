import type { BookEvent } from "@storymark/shared-types";
import type { CachedResult } from "@/api/read-through-cache";
import { positionAwareCache } from "@/api/read-through-cache";
import type { BookVersion } from "@/db/storymark-db";
import { db } from "@/db/storymark-db";

export const eventsApi = {
  /** Events visible at the reading position, for the timeline. Read-through Dexie
   * cache (see {@link positionAwareCache}): gated by the reader's boundary and the
   * book's `version`, and a stale row is still returned (as `stale-offline`) on a
   * network failure so the timeline works offline. `canCache` is false while the
   * book is still processing, so an incomplete list isn't written back. Throws
   * {@link ApiError} only when nothing usable is cached. */
  list: (
    bookId: string,
    upToSection: number,
    upToLocation: number,
    version: BookVersion | null,
    canCache: boolean,
  ): Promise<CachedResult<readonly BookEvent[]>> =>
    positionAwareCache({
      bookId,
      upToSection,
      upToLocation,
      version,
      canCache,
      resource: "events",
      errorMessage: "Failed to load events",
      table: db.timelines,
      pluck: (row) => row.events,
      pack: (events, base) => ({ ...base, events }),
    }),
} as const;

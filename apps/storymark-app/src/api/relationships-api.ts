import type { Relationship } from "@storymark/shared-types";
import type { CachedResult } from "@/api/read-through-cache";
import { positionAwareCache } from "@/api/read-through-cache";
import type { BookVersion } from "@/db/storymark-db";
import { db } from "@/db/storymark-db";

export const relationshipsApi = {
  /** Relationships visible at the reading position, for the graph. Read-through Dexie
   * cache (see {@link positionAwareCache}): gated by the reader's boundary and the
   * book's `version`, and a stale row is still returned (as `stale-offline`) on a
   * network failure so the graph works offline. `canCache` is false while the book is
   * still processing. Throws {@link ApiError} only when nothing usable is cached. */
  list: (
    bookId: string,
    upToSection: number,
    upToLocation: number,
    version: BookVersion | null,
    canCache: boolean,
  ): Promise<CachedResult<readonly Relationship[]>> =>
    positionAwareCache({
      bookId,
      upToSection,
      upToLocation,
      version,
      canCache,
      resource: "relationships",
      errorMessage: "Failed to load relationships",
      table: db.graphs,
      pluck: (row) => row.relationships,
      pack: (relationships, base) => ({ ...base, relationships }),
    }),
} as const;

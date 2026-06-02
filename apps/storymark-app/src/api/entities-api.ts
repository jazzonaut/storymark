import type { EntitySummary, StoryGuideEntity } from "@storymark/shared-types";
import { fetchJson, LLM_TIMEOUT_MS } from "@/api/http";
import type { CachedResult } from "@/api/read-through-cache";
import { positionAwareCache } from "@/api/read-through-cache";
import type { BookVersion } from "@/db/storymark-db";
import { db, purgeEntityDerivedCache } from "@/db/storymark-db";

export const entitiesApi = {
  /** The Story Guide list — entities visible at the reading position. Read-through
   * Dexie cache (see {@link positionAwareCache}): gated by the reader's boundary and
   * the book's `version`, and a stale row is still returned (as `stale-offline`) on a
   * network failure, so the Story Guide, the timeline's name lookups, and the graph's
   * nodes all survive offline. `canCache` is false while the book is still processing.
   * Throws {@link ApiError} only when nothing usable is cached. */
  list: (
    bookId: string,
    upToSection: number,
    upToLocation: number,
    version: BookVersion | null,
    canCache: boolean,
  ): Promise<CachedResult<readonly StoryGuideEntity[]>> =>
    positionAwareCache({
      bookId,
      upToSection,
      upToLocation,
      version,
      canCache,
      resource: "entities",
      errorMessage: "Failed to load entities",
      table: db.entities,
      pluck: (row) => row.entities,
      pack: (entities, base) => ({ ...base, entities }),
    }),

  /** A spoiler-safe summary for one entity at the reading position; the server
   * generates it on first request (~1–3s on local Ollama, occasionally longer) and
   * caches it — hence the wider timeout. Not cached client-side (it can't be
   * generated offline). Throws {@link ApiError} on failure (incl. offline, and 404
   * for an unmet entity). */
  summary: (
    bookId: string,
    entityId: string,
    section: number,
    location: number,
  ): Promise<EntitySummary> =>
    fetchJson<EntitySummary>(
      `/api/books/${encodeURIComponent(bookId)}/entities/${encodeURIComponent(entityId)}/summary?section=${String(section)}&location=${String(location)}`,
      "Failed to load summary",
      {},
      LLM_TIMEOUT_MS,
    ),

  /** Folds one or more duplicate entities into a survivor (which keeps its canonical
   * name); the losers' names become its aliases. Returns the updated survivor. On success
   * the book's cached entity-derived surfaces (Story Guide, timeline, graph) are dropped so
   * they refetch the merged shape. Throws {@link ApiError} on failure (incl. 404 for an
   * unknown book/survivor/loser). Server-only: a merge can't be performed offline. */
  merge: async (
    bookId: string,
    survivorId: string,
    loserIds: readonly string[],
  ): Promise<StoryGuideEntity> => {
    const survivor = await fetchJson<StoryGuideEntity>(
      `/api/books/${encodeURIComponent(bookId)}/entities/${encodeURIComponent(survivorId)}/merge`,
      "Failed to merge entities",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ loserIds }),
      },
    );
    await purgeEntityDerivedCache(bookId);
    return survivor;
  },

  /** Reflects a freshly generated entity summary into the cached Story Guide list
   * row for its section, so the list row stops showing the unrevealed state and
   * stays revealed if the panel is reopened within the cache TTL. No-op when that
   * section's list isn't cached yet. */
  cacheListSummary: async (
    bookId: string,
    upToSection: number,
    entityId: string,
    shortSummary: string,
  ): Promise<void> => {
    const row = await db.entities.get([bookId, upToSection]);
    if (!row) {
      return;
    }
    await db.entities.put({
      ...row,
      entities: row.entities.map((entity) =>
        entity.entityId === entityId ? { ...entity, shortSummary } : entity,
      ),
    });
  },
} as const;

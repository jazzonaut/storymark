import type { EntitySummary, StoryGuideEntity } from "@storymark/shared-types";
import { fetchJson, LLM_TIMEOUT_MS } from "@/api/http";
import { readThroughCache } from "@/api/read-through-cache";
import { db } from "@/db/storymark-db";

export const entitiesApi = {
  /** The Story Guide list — entities visible at the reading position. Read-through
   * Dexie cache (see {@link readThroughCache}): a stale row is still returned on a
   * network failure, so the Story Guide, the timeline's name lookups, and the graph's
   * nodes all survive offline. Throws {@link ApiError} only when nothing is cached. */
  list: (
    bookId: string,
    upToSection: number,
    upToLocation: number,
  ): Promise<readonly StoryGuideEntity[]> =>
    readThroughCache({
      readCached: async () => {
        const row = await db.entities.get([bookId, upToSection]);
        return row ? { value: row.entities, cachedAt: row.cachedAt } : undefined;
      },
      fetchFresh: () =>
        fetchJson<readonly StoryGuideEntity[]>(
          `/api/books/${encodeURIComponent(bookId)}/entities?upToSection=${String(upToSection)}&upToLocation=${String(upToLocation)}`,
          "Failed to load entities",
        ),
      writeBack: (entities) =>
        db.entities.put({ bookId, upToSection, entities, cachedAt: Date.now() }),
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

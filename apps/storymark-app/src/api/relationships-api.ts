import type { Relationship } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { db } from "@/db/storymark-db";

const TTL_MS = 5 * 60 * 1000;

async function fetchRelationships(
  bookId: string,
  upToSection: number,
  upToLocation: number,
): Promise<readonly Relationship[]> {
  const response = await fetch(
    `/api/books/${encodeURIComponent(bookId)}/relationships?upToSection=${String(upToSection)}&upToLocation=${String(upToLocation)}`,
  );
  if (!response.ok) {
    throw new ApiError(response.status, `Failed to load relationships (${response.status}).`);
  }
  return (await response.json()) as readonly Relationship[];
}

export const relationshipsApi = {
  /** Relationships visible at the reading position, for the graph. Read-through: a
   * fresh Dexie row (keyed by section, short TTL) is served directly; otherwise the
   * network result is cached. On a network failure a stale cached row is still
   * returned so the graph works offline. Throws {@link ApiError} only when nothing
   * is cached. */
  list: async (
    bookId: string,
    upToSection: number,
    upToLocation: number,
  ): Promise<readonly Relationship[]> => {
    const cached = await db.graphs.get([bookId, upToSection]);
    if (cached && Date.now() - cached.cachedAt < TTL_MS) {
      return cached.relationships;
    }
    try {
      const relationships = await fetchRelationships(bookId, upToSection, upToLocation);
      await db.graphs.put({ bookId, upToSection, relationships, cachedAt: Date.now() });
      return relationships;
    } catch (caught) {
      if (cached) {
        return cached.relationships;
      }
      throw caught;
    }
  },
} as const;

import type { EntitySummary, StoryGuideEntity } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";

export const entitiesApi = {
  /** The Story Guide list — entities visible at the reading position. Throws
   * {@link ApiError} on failure (incl. offline). */
  list: async (
    bookId: string,
    upToSection: number,
    upToLocation: number,
  ): Promise<readonly StoryGuideEntity[]> => {
    const response = await fetch(
      `/api/books/${encodeURIComponent(bookId)}/entities?upToSection=${String(upToSection)}&upToLocation=${String(upToLocation)}`,
    );
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to load entities (${response.status}).`);
    }
    return (await response.json()) as readonly StoryGuideEntity[];
  },

  /** A spoiler-safe summary for one entity at the reading position; the server
   * generates it on first request (~1–3s on local Ollama) and caches it. Throws
   * {@link ApiError} on failure (incl. offline, and 404 for an unmet entity). */
  summary: async (
    bookId: string,
    entityId: string,
    section: number,
    location: number,
  ): Promise<EntitySummary> => {
    const response = await fetch(
      `/api/books/${encodeURIComponent(bookId)}/entities/${encodeURIComponent(entityId)}/summary?section=${String(section)}&location=${String(location)}`,
    );
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to load summary (${response.status}).`);
    }
    return (await response.json()) as EntitySummary;
  },
} as const;

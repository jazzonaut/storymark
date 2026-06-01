import type { BookEvent } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { db } from "@/db/storymark-db";

const TTL_MS = 5 * 60 * 1000;

async function fetchEvents(
  bookId: string,
  upToSection: number,
  upToLocation: number,
): Promise<readonly BookEvent[]> {
  const response = await fetch(
    `/api/books/${encodeURIComponent(bookId)}/events?upToSection=${String(upToSection)}&upToLocation=${String(upToLocation)}`,
  );
  if (!response.ok) {
    throw new ApiError(response.status, `Failed to load events (${response.status}).`);
  }
  return (await response.json()) as readonly BookEvent[];
}

export const eventsApi = {
  /** Events visible at the reading position, for the timeline. Read-through: a fresh
   * Dexie row (keyed by section, short TTL) is served directly; otherwise the network
   * result is cached. On a network failure a stale cached row is still returned so the
   * timeline works offline. Throws {@link ApiError} only when nothing is cached. */
  list: async (
    bookId: string,
    upToSection: number,
    upToLocation: number,
  ): Promise<readonly BookEvent[]> => {
    const cached = await db.timelines.get([bookId, upToSection]);
    if (cached && Date.now() - cached.cachedAt < TTL_MS) {
      return cached.events;
    }
    try {
      const events = await fetchEvents(bookId, upToSection, upToLocation);
      await db.timelines.put({ bookId, upToSection, events, cachedAt: Date.now() });
      return events;
    } catch (caught) {
      if (cached) {
        return cached.events;
      }
      throw caught;
    }
  },
} as const;

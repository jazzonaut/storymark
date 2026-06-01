import type { Section, SectionSummary } from "@storymark/shared-types";
import { fetchJson } from "@/api/http";
import { readThroughCache } from "@/api/read-through-cache";
import { db } from "@/db/storymark-db";

// The single read path for reader content. Reads through IndexedDB first, falls
// back to the API, and writes back — so once a book is prefetched the reader is
// fully offline-capable (the offline-detection/service-worker layer lands in S2.3).
export const readerApi = {
  // TTL read-through (not cache-forever): the manifest carries section titles, which
  // the server can change after the fact (a title backfill), so a stale row is
  // refreshed when online and still served offline.
  getManifest: async (bookId: string): Promise<readonly SectionSummary[]> =>
    (
      await readThroughCache({
        readCached: async () => {
          const row = await db.manifests.get(bookId);
          return row ? { value: row.sections, cachedAt: row.cachedAt } : undefined;
        },
        fetchFresh: () =>
          fetchJson<readonly SectionSummary[]>(
            `/api/books/${encodeURIComponent(bookId)}/sections`,
            "Failed to load sections",
          ),
        writeBack: (sections) => db.manifests.put({ bookId, sections, cachedAt: Date.now() }),
      })
    ).value,

  getSection: async (bookId: string, sectionIndex: number): Promise<Section> => {
    const cached = await db.sections.get([bookId, sectionIndex]);
    if (cached) {
      return cached;
    }
    const section = await fetchJson<Section>(
      `/api/books/${encodeURIComponent(bookId)}/sections/${String(sectionIndex)}`,
      `Failed to load section ${String(sectionIndex)}`,
    );
    await db.sections.put({ ...section, bookId });
    return section;
  },
} as const;

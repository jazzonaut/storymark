import type { Section, SectionSummary } from "@storymark/shared-types";
import { fetchJson } from "@/api/http";
import { db } from "@/db/storymark-db";

// The single read path for reader content. Reads through IndexedDB first, falls
// back to the API, and writes back — so once a book is prefetched the reader is
// fully offline-capable (the offline-detection/service-worker layer lands in S2.3).
export const readerApi = {
  getManifest: async (bookId: string): Promise<readonly SectionSummary[]> => {
    const cached = await db.manifests.get(bookId);
    if (cached) {
      return cached.sections;
    }
    const sections = await fetchJson<readonly SectionSummary[]>(
      `/api/books/${encodeURIComponent(bookId)}/sections`,
      "Failed to load sections",
    );
    await db.manifests.put({ bookId, sections });
    return sections;
  },

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

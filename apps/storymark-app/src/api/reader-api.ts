import type { Section, SectionSummary } from "@storymark/shared-types";
import { fetchJson } from "@/api/http";
import { readThroughCache, versionTag } from "@/api/read-through-cache";
import type { BookVersion } from "@/db/storymark-db";
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

  // Version-gated read-through (like the memory surfaces, not cache-forever): a cached
  // row whose `version` still matches is served without a refetch, a stale-versioned row
  // is refetched (falling back to the old row offline), and a missing row is fetched and
  // written back. Section content is location-independent and stable for a given version,
  // so the gate is version-only — no TTL. `canCache` is false while the book is still
  // processing, so an incomplete entities array is never written back.
  getSection: async (
    bookId: string,
    sectionIndex: number,
    version: BookVersion | null,
    canCache: boolean,
  ): Promise<Section> =>
    (
      await readThroughCache<Section>({
        version,
        canCache,
        readCached: async () => {
          const row = await db.sections.get([bookId, sectionIndex]);
          return row ? { value: row, ...versionTag(row.version) } : undefined;
        },
        fetchFresh: () =>
          fetchJson<Section>(
            `/api/books/${encodeURIComponent(bookId)}/sections/${String(sectionIndex)}`,
            `Failed to load section ${String(sectionIndex)}`,
          ),
        writeBack: (section) => db.sections.put({ ...section, bookId, ...versionTag(version) }),
      })
    ).value,
} as const;

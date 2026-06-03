import type { Section, SectionSummary } from "@storymark/shared-types";
import { fetchJson } from "@/api/http";
import { readThroughCache, versionTag } from "@/api/read-through-cache";
import type { BookVersion } from "@/db/storymark-db";
import { db } from "@/db/storymark-db";

// The single read path for reader content. Reads through IndexedDB first, falls
// back to the API, and writes back — so once a book is prefetched the reader is
// fully offline-capable (the offline-detection/service-worker layer lands in S2.3).
export const readerApi = {
  // TTL + version read-through (not cache-forever): the TTL catches server-side
  // title changes (a title backfill), and the version gate catches a reprocess —
  // which can change the section *count*, something a TTL-only row would keep
  // serving (indefinitely offline) while the version-gated section caches had
  // already invalidated. The write-back preserves `imagesWarmedForVersion`: the
  // warmed marker outlives a TTL refresh, and a version change naturally restarts
  // the warming because the stored marker no longer matches.
  getManifest: async (
    bookId: string,
    version: BookVersion | null,
  ): Promise<readonly SectionSummary[]> =>
    (
      await readThroughCache({
        version,
        readCached: async () => {
          const row = await db.manifests.get(bookId);
          return row
            ? { value: row.sections, cachedAt: row.cachedAt, ...versionTag(row.version) }
            : undefined;
        },
        fetchFresh: () =>
          fetchJson<readonly SectionSummary[]>(
            `/api/books/${encodeURIComponent(bookId)}/sections`,
            "Failed to load sections",
          ),
        writeBack: async (sections) => {
          const existing = await db.manifests.get(bookId);
          await db.manifests.put({
            bookId,
            sections,
            cachedAt: Date.now(),
            ...versionTag(version),
            ...(existing?.imagesWarmedForVersion === undefined
              ? {}
              : { imagesWarmedForVersion: existing.imagesWarmedForVersion }),
          });
        },
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

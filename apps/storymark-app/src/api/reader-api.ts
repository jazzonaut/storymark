import type { Section, SectionSummary } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { db } from "@/db/storymark-db";

// A mobile connection can leave a fetch half-open with no response and no
// error, which would hang the read path indefinitely (e.g. the first-open cache
// loop never settling). Abort after this so the caller sees a failure it can
// retry instead of a permanent spinner.
const REQUEST_TIMEOUT_MS = 20_000;

async function fetchReader(path: string): Promise<Response> {
  return fetch(path, { signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
}

async function fetchManifest(bookId: string): Promise<readonly SectionSummary[]> {
  const response = await fetchReader(`/api/books/${encodeURIComponent(bookId)}/sections`);
  if (!response.ok) {
    throw new ApiError(response.status, `Failed to load sections (${response.status}).`);
  }
  return (await response.json()) as readonly SectionSummary[];
}

async function fetchSection(bookId: string, sectionIndex: number): Promise<Section> {
  const response = await fetchReader(
    `/api/books/${encodeURIComponent(bookId)}/sections/${String(sectionIndex)}`,
  );
  if (!response.ok) {
    throw new ApiError(
      response.status,
      `Failed to load section ${String(sectionIndex)} (${response.status}).`,
    );
  }
  return (await response.json()) as Section;
}

// The single read path for reader content. Reads through IndexedDB first, falls
// back to the API, and writes back — so once a book is prefetched the reader is
// fully offline-capable (the offline-detection/service-worker layer lands in S2.3).
export const readerApi = {
  getManifest: async (bookId: string): Promise<readonly SectionSummary[]> => {
    const cached = await db.manifests.get(bookId);
    if (cached) {
      return cached.sections;
    }
    const sections = await fetchManifest(bookId);
    await db.manifests.put({ bookId, sections });
    return sections;
  },

  getSection: async (bookId: string, sectionIndex: number): Promise<Section> => {
    const cached = await db.sections.get([bookId, sectionIndex]);
    if (cached) {
      return cached;
    }
    const section = await fetchSection(bookId, sectionIndex);
    await db.sections.put({ ...section, bookId });
    return section;
  },
} as const;

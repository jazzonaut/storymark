import { ref } from "vue";
import type { Ref } from "vue";
import type { Section } from "@storymark/shared-types";
import { apiFetch } from "@/api/http";
import { readerApi } from "@/api/reader-api";
import { useBooks } from "@/composables/use-books";
import type { BookVersion } from "@/db/storymark-db";
import { db } from "@/db/storymark-db";

const BATCH_SIZE = 4;
const IMAGE_BATCH_SIZE = 8;

export interface CacheProgress {
  readonly cached: number;
  readonly total: number;
}

const progress = ref<CacheProgress | null>(null);
const isCaching = ref(false);

interface UseBookCache {
  readonly progress: Ref<CacheProgress | null>;
  readonly isCaching: Ref<boolean>;
  ensureCached: (bookId: string) => Promise<void>;
}

function extractImageUrls(bookId: string, section: Section): readonly string[] {
  const document = new DOMParser().parseFromString(section.html, "text/html");
  const prefix = `/api/books/${bookId}/resources/`;
  const urls = new Set<string>();

  for (const image of document.querySelectorAll("img[src]")) {
    const raw = image.getAttribute("src");
    if (raw === null) {
      continue;
    }

    const url = new URL(raw, window.location.origin);
    if (url.origin === window.location.origin && url.pathname.startsWith(prefix)) {
      urls.add(url.pathname + url.search);
    }
  }

  return [...urls];
}

async function warmImageCache(urls: readonly string[]): Promise<void> {
  for (let start = 0; start < urls.length; start += IMAGE_BATCH_SIZE) {
    const batch = urls.slice(start, start + IMAGE_BATCH_SIZE);
    // Batches run sequentially to avoid opening a connection per image in image-heavy books.
    // apiFetch rather than bare fetch: a 401 here (session died mid-warm) then
    // raises the auth-expired signal instead of silently failing every image.
    // eslint-disable-next-line no-await-in-loop
    await Promise.all(batch.map((url) => apiFetch(url).catch(() => undefined)));
  }
}

/** Pushes every inline image referenced by the cached sections into the service
 * worker's CacheFirst `book-images` cache, so offline reading has images and not
 * just HTML. Runs *after* the section download (never inside its batch slots —
 * an image-heavy book would otherwise gate section progress on image fetches)
 * and records the warmed version on the manifest row, so re-opening an
 * already-cached book skips the per-section DOM parse and per-image fetch. */
async function warmImagesOnce(
  bookId: string,
  version: BookVersion | null,
  sections: readonly Section[],
): Promise<void> {
  const manifestRow = await db.manifests.get(bookId);
  if (version !== null && manifestRow?.imagesWarmedForVersion === version) {
    return;
  }

  const urls = new Set(sections.flatMap((section) => extractImageUrls(bookId, section)));
  await warmImageCache([...urls]);

  if (version !== null && manifestRow !== undefined) {
    await db.manifests.put({ ...manifestRow, imagesWarmedForVersion: version });
  }
}

/** Downloads a book's whole manifest + every section into IndexedDB on first
 * open (parallel batches of {@link BATCH_SIZE}), exposing reactive progress,
 * then warms the inline-image cache for offline reading. Re-opening an
 * already-cached book resolves from IndexedDB without network. */
export function useBookCache(): UseBookCache {
  const { cacheParams } = useBooks();

  async function ensureCached(bookId: string): Promise<void> {
    isCaching.value = true;
    try {
      const { version, canCache } = cacheParams(bookId);
      const manifest = await readerApi.getManifest(bookId, version);
      const total = manifest.length;
      let cached = 0;
      progress.value = { cached, total };

      const sections: Section[] = [];
      for (let start = 0; start < total; start += BATCH_SIZE) {
        const batch = manifest.slice(start, start + BATCH_SIZE);
        // Batches run sequentially on purpose: bounded concurrency keeps the
        // download server-friendly (the alternative the lint rule suggests —
        // firing every section at once — is exactly what we avoid).
        // eslint-disable-next-line no-await-in-loop
        const fetched = await Promise.all(
          batch.map((section) =>
            readerApi.getSection(bookId, section.sectionIndex, version, canCache),
          ),
        );
        sections.push(...fetched);
        cached += batch.length;
        progress.value = { cached, total };
      }

      await warmImagesOnce(bookId, version, sections);
    } finally {
      isCaching.value = false;
    }
  }

  return { progress, isCaching, ensureCached };
}

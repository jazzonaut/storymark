import { ref } from "vue";
import type { Ref } from "vue";
import { readerApi } from "@/api/reader-api";
import { useBooks } from "@/composables/use-books";

const BATCH_SIZE = 4;

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

/** Downloads a book's whole manifest + every section into IndexedDB on first
 * open (parallel batches of {@link BATCH_SIZE}), exposing reactive progress.
 * Re-opening an already-cached book resolves immediately. */
export function useBookCache(): UseBookCache {
  const { cacheParams } = useBooks();

  async function ensureCached(bookId: string): Promise<void> {
    isCaching.value = true;
    try {
      const { version, canCache } = cacheParams(bookId);
      const manifest = await readerApi.getManifest(bookId);
      const total = manifest.length;
      let cached = 0;
      progress.value = { cached, total };

      for (let start = 0; start < total; start += BATCH_SIZE) {
        const batch = manifest.slice(start, start + BATCH_SIZE);
        // Batches run sequentially on purpose: bounded concurrency keeps the
        // download server-friendly (the alternative the lint rule suggests —
        // firing every section at once — is exactly what we avoid).
        // eslint-disable-next-line no-await-in-loop
        await Promise.all(
          batch.map((section) =>
            readerApi.getSection(bookId, section.sectionIndex, version, canCache),
          ),
        );
        cached += batch.length;
        progress.value = { cached, total };
      }
    } finally {
      isCaching.value = false;
    }
  }

  return { progress, isCaching, ensureCached };
}

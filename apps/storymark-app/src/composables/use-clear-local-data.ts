import { ref } from "vue";
import type { Ref } from "vue";
import { clearCachedData, hasUnsyncedChanges } from "@/db/storymark-db";
import { useBookmarks } from "@/composables/use-bookmarks";
import { useReadingProgress } from "@/composables/use-reading-progress";

/** Thrown when queued reading-position/bookmark writes couldn't be synced, so
 * clearing would discard them. The caller keeps the local data and surfaces this. */
export class UnsyncedChangesError extends Error {
  constructor() {
    super("unsynced changes remain");
    this.name = "UnsyncedChangesError";
  }
}

// The service worker's runtime caches hold downloaded book content (covers, images,
// the book list). The workbox precache holds the app shell itself — leaving it intact
// keeps the app loadable offline after the post-clear reload, so it is preserved.
async function clearContentCaches(): Promise<void> {
  if (!("caches" in window)) {
    return;
  }
  const keys = await caches.keys();
  await Promise.all(
    keys.filter((key) => !key.startsWith("workbox-precache")).map((key) => caches.delete(key)),
  );
}

interface UseClearLocalData {
  readonly isClearing: Ref<boolean>;
  clear: () => Promise<void>;
}

export function useClearLocalData(): UseClearLocalData {
  const isClearing = ref(false);
  const { flush: flushProgress } = useReadingProgress();
  const { flush: flushBookmarks } = useBookmarks();

  // Flush the sync queue to the server, clear all cached/queue tables and the content
  // caches, then hard-navigate to the library so everything re-hydrates from a clean
  // slate. Aborts (keeping local data) if anything is still unsynced afterwards.
  async function clear(): Promise<void> {
    if (isClearing.value) {
      return;
    }
    isClearing.value = true;
    try {
      await flushProgress();
      await flushBookmarks();
      if (await hasUnsyncedChanges()) {
        throw new UnsyncedChangesError();
      }
      await clearCachedData();
      await clearContentCaches();
      window.location.assign("/");
    } finally {
      isClearing.value = false;
    }
  }

  return { isClearing, clear };
}

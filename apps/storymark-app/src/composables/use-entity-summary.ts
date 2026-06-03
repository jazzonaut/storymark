import { shallowRef } from "vue";
import type { ShallowRef } from "vue";
import type { EntitySummary } from "@storymark/shared-types";
import { entitiesApi } from "@/api/entities-api";
import { ApiError } from "@/api/api-error";
import { isTimeout } from "@/api/http";
import type { BookVersion } from "@/db/storymark-db";
import { useCachedLoad } from "@/composables/use-cached-load";
import type { CachedLoadState } from "@/composables/use-cached-load";

/** The entity whose popup is open: its id and the display name to show while the
 * summary is still generating. */
export interface OpenEntity {
  readonly entityId: string;
  readonly name: string;
}

// Distinct messages per failure mode so a stuck popup is diagnosable instead of one
// catch-all: an HTTP status means the request reached a server (the status pins it on
// the API vs. the reverse proxy); a timeout or a thrown TypeError means it never got a
// response (slow link or offline). The status code is shown so a phone-only failure
// can be read off the screen without remote-debugging. Only reached when nothing is
// cached — with a cached copy the popup shows it under a refresh-status badge instead.
function describeError(caught: unknown): string {
  if (caught instanceof ApiError) {
    if (caught.status === 404) {
      return "There's nothing to show for this yet at your place in the story.";
    }
    if (caught.status >= 500) {
      return `The summary couldn't be generated just now (${String(caught.status)}). Try again in a moment.`;
    }
    return `Couldn't load this just now (${String(caught.status)}).`;
  }
  if (isTimeout(caught)) {
    return "The summary took too long to load. Try again.";
  }
  return "Story Guide is unavailable offline.";
}

// Module-level: one entity popup is open at a time (like the selection popover).
const openEntity = shallowRef<OpenEntity | null>(null);
const summary = shallowRef<EntitySummary | null>(null);
// Tapping entity B before entity A's request resolves must not let A's slow response
// populate B's popup — the loader gates every step on its load still being current.
const loader = useCachedLoad();
const { isLoading, isRefreshing, error, fallback } = loader;

interface UseEntitySummary extends CachedLoadState {
  readonly openEntity: ShallowRef<OpenEntity | null>;
  readonly summary: ShallowRef<EntitySummary | null>;
  open: (
    bookId: string,
    entityId: string,
    name: string,
    section: number,
    location: number,
    version: BookVersion | null,
    canCache: boolean,
  ) => Promise<EntitySummary | null>;
  close: () => void;
}

export function useEntitySummary(): UseEntitySummary {
  function open(
    bookId: string,
    entityId: string,
    name: string,
    section: number,
    location: number,
    version: BookVersion | null,
    canCache: boolean,
  ): Promise<EntitySummary | null> {
    openEntity.value = { entityId, name };
    summary.value = null;
    return loader.run({
      load: async (ctx) => {
        const result = await entitiesApi.summary(
          bookId,
          entityId,
          section,
          location,
          version,
          canCache,
          ctx.showCached((cached) => {
            summary.value = cached;
          }),
        );
        if (ctx.isCurrent()) {
          summary.value = result.value;
          ctx.commit(result);
        }
        return result.value;
      },
      describeError,
    });
  }

  function close(): void {
    // Supersede any in-flight request so it can't repopulate after close.
    loader.invalidate();
    openEntity.value = null;
    summary.value = null;
  }

  return { openEntity, summary, isLoading, isRefreshing, error, fallback, open, close };
}

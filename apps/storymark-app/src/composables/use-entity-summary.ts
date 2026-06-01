import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";
import type { EntitySummary } from "@storymark/shared-types";
import { entitiesApi } from "@/api/entities-api";
import { ApiError } from "@/api/api-error";

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
// can be read off the screen without remote-debugging.
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
  if (caught instanceof DOMException && caught.name === "TimeoutError") {
    return "The summary took too long to load. Try again.";
  }
  return "Story Guide is unavailable offline.";
}

// Module-level: one entity popup is open at a time (like the selection popover).
const openEntity = shallowRef<OpenEntity | null>(null);
const isLoading = ref(false);
const summary = shallowRef<EntitySummary | null>(null);
const error = ref<string | null>(null);

interface UseEntitySummary {
  readonly openEntity: ShallowRef<OpenEntity | null>;
  readonly isLoading: Ref<boolean>;
  readonly summary: ShallowRef<EntitySummary | null>;
  readonly error: Ref<string | null>;
  open: (
    bookId: string,
    entityId: string,
    name: string,
    section: number,
    location: number,
  ) => Promise<EntitySummary | null>;
  close: () => void;
}

export function useEntitySummary(): UseEntitySummary {
  async function open(
    bookId: string,
    entityId: string,
    name: string,
    section: number,
    location: number,
  ): Promise<EntitySummary | null> {
    openEntity.value = { entityId, name };
    isLoading.value = true;
    summary.value = null;
    error.value = null;
    try {
      const loaded = await entitiesApi.summary(bookId, entityId, section, location);
      summary.value = loaded;
      return loaded;
    } catch (caught) {
      error.value = describeError(caught);
      return null;
    } finally {
      isLoading.value = false;
    }
  }

  function close(): void {
    openEntity.value = null;
    isLoading.value = false;
    summary.value = null;
    error.value = null;
  }

  return { openEntity, isLoading, summary, error, open, close };
}

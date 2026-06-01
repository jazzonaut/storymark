import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";
import type { BookEvent, StoryGuideEntity } from "@storymark/shared-types";
import { eventsApi } from "@/api/events-api";
import { entitiesApi } from "@/api/entities-api";
import { ApiError } from "@/api/api-error";
import type { BookVersion } from "@/db/storymark-db";
import { useLatestOnly } from "@/composables/use-latest-only";

// Module-level: one reader (one timeline) is open at a time.
const events = shallowRef<readonly BookEvent[]>([]);
const entityNames = shallowRef<ReadonlyMap<string, string>>(new Map());
const isLoading = ref(false);
const error = ref<string | null>(null);
const isStale = ref(false);
// A slow earlier fetch (e.g. a quick page-turn ahead of a stale response) must not
// overwrite the current boundary's data, so each load commits only if it's the latest.
const latest = useLatestOnly();

interface UseTimeline {
  readonly events: ShallowRef<readonly BookEvent[]>;
  readonly entityNames: ShallowRef<ReadonlyMap<string, string>>;
  readonly isLoading: Ref<boolean>;
  readonly error: Ref<string | null>;
  readonly isStale: Ref<boolean>;
  load: (
    bookId: string,
    section: number,
    location: number,
    version: BookVersion | null,
    canCache: boolean,
  ) => Promise<void>;
}

export function useTimeline(): UseTimeline {
  async function load(
    bookId: string,
    section: number,
    location: number,
    version: BookVersion | null,
    canCache: boolean,
  ): Promise<void> {
    const token = latest.begin();
    isLoading.value = true;
    error.value = null;
    try {
      // Events drive the chart; the entity list only resolves involved-entity ids to
      // display names for tooltips/detail. Names are best-effort, so the entity fetch
      // fails soft to an empty map — otherwise its failure (e.g. a cold cache offline)
      // would reject the pair and discard the cached events the timeline can still show.
      const [eventsResult, entities] = await Promise.all([
        eventsApi.list(bookId, section, location, version, canCache),
        entitiesApi
          .list(bookId, section, location, version, canCache)
          .then((result) => result.value)
          .catch((): readonly StoryGuideEntity[] => []),
      ]);
      if (!latest.isCurrent(token)) {
        return;
      }
      events.value = eventsResult.value;
      entityNames.value = new Map(
        entities.map((entity) => [entity.entityId, entity.canonicalName]),
      );
      isStale.value = eventsResult.status === "stale-offline";
    } catch (caught) {
      if (!latest.isCurrent(token)) {
        return;
      }
      error.value =
        caught instanceof ApiError
          ? "Couldn't load the timeline just now."
          : "The timeline is unavailable offline.";
      events.value = [];
      entityNames.value = new Map();
      isStale.value = false;
    } finally {
      if (latest.isCurrent(token)) {
        isLoading.value = false;
      }
    }
  }

  return { events, entityNames, isLoading, error, isStale, load };
}

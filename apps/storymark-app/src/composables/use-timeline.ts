import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";
import type { BookEvent, StoryGuideEntity } from "@storymark/shared-types";
import { eventsApi } from "@/api/events-api";
import { entitiesApi } from "@/api/entities-api";
import { ApiError } from "@/api/api-error";

// Module-level: one reader (one timeline) is open at a time.
const events = shallowRef<readonly BookEvent[]>([]);
const entityNames = shallowRef<ReadonlyMap<string, string>>(new Map());
const isLoading = ref(false);
const error = ref<string | null>(null);

interface UseTimeline {
  readonly events: ShallowRef<readonly BookEvent[]>;
  readonly entityNames: ShallowRef<ReadonlyMap<string, string>>;
  readonly isLoading: Ref<boolean>;
  readonly error: Ref<string | null>;
  load: (bookId: string, section: number, location: number) => Promise<void>;
}

export function useTimeline(): UseTimeline {
  async function load(bookId: string, section: number, location: number): Promise<void> {
    isLoading.value = true;
    error.value = null;
    try {
      // Events drive the chart; the entity list only resolves involved-entity ids to
      // display names for tooltips/detail. Names are best-effort, so the entity fetch
      // fails soft to an empty map — otherwise its failure (e.g. a cold cache offline)
      // would reject the pair and discard the cached events the timeline can still show.
      const [loadedEvents, entities] = await Promise.all([
        eventsApi.list(bookId, section, location),
        entitiesApi.list(bookId, section, location).catch((): readonly StoryGuideEntity[] => []),
      ]);
      events.value = loadedEvents;
      entityNames.value = new Map(
        entities.map((entity) => [entity.entityId, entity.canonicalName]),
      );
    } catch (caught) {
      error.value =
        caught instanceof ApiError
          ? "Couldn't load the timeline just now."
          : "The timeline is unavailable offline.";
      events.value = [];
      entityNames.value = new Map();
    } finally {
      isLoading.value = false;
    }
  }

  return { events, entityNames, isLoading, error, load };
}

import { shallowRef } from "vue";
import type { ShallowRef } from "vue";
import type { BookEvent, StoryGuideEntity } from "@storymark/shared-types";
import { eventsApi } from "@/api/events-api";
import { entitiesApi } from "@/api/entities-api";
import type { BookVersion } from "@/db/storymark-db";
import { describeLoadError, useCachedLoad } from "@/composables/use-cached-load";
import type { CachedLoadState } from "@/composables/use-cached-load";

// Module-level: one reader (one timeline) is open at a time.
const events = shallowRef<readonly BookEvent[]>([]);
const entityNames = shallowRef<ReadonlyMap<string, string>>(new Map());

function toNameMap(entities: readonly StoryGuideEntity[]): ReadonlyMap<string, string> {
  return new Map(entities.map((entity) => [entity.entityId, entity.canonicalName]));
}

const loader = useCachedLoad();
const { isLoading, isRefreshing, error, fallback } = loader;

interface UseTimeline extends CachedLoadState {
  readonly events: ShallowRef<readonly BookEvent[]>;
  readonly entityNames: ShallowRef<ReadonlyMap<string, string>>;
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
    await loader.run({
      load: async (ctx) => {
        // The name map is position-keyed like the events: a previous load's map could
        // resolve names from a boundary ahead of the reader (after a backward jump),
        // so it is cleared up front rather than left to label the new events.
        entityNames.value = new Map();
        // Events drive the chart; the entity list only resolves involved-entity ids to
        // display names for tooltips/detail. Names are best-effort, so the entity fetch
        // fails soft to an empty map — otherwise its failure (e.g. a cold cache offline)
        // would reject the pair and discard the cached events the timeline can still show.
        // Events are the primary surface, so a boundary-safe cached copy is shown (with a
        // refreshing badge) while the live fetch runs, instead of blanking to a skeleton.
        const [eventsResult, entities] = await Promise.all([
          eventsApi.list(
            bookId,
            section,
            location,
            version,
            canCache,
            ctx.showCached((cached) => {
              events.value = cached;
            }),
          ),
          entitiesApi
            .list(bookId, section, location, version, canCache, (cached) => {
              // Cached names label the cached events under the refreshing badge.
              // Deliberately not ctx.showCached: names alone must not lift the
              // skeleton — that would expose events not yet painted for this load.
              if (ctx.isCurrent()) {
                entityNames.value = toNameMap(cached);
              }
            })
            .then((result) => result.value)
            .catch((): readonly StoryGuideEntity[] => []),
        ]);
        if (!ctx.isCurrent()) {
          return;
        }
        events.value = eventsResult.value;
        entityNames.value = toNameMap(entities);
        ctx.commit(eventsResult);
      },
      describeError: describeLoadError(
        "Couldn't load the timeline just now.",
        "The timeline is unavailable offline.",
      ),
      onFailure: () => {
        events.value = [];
        entityNames.value = new Map();
      },
    });
  }

  return { events, entityNames, isLoading, isRefreshing, error, fallback, load };
}

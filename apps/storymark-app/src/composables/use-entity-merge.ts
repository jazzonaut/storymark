import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";

/** The entity a merge will fold others into: the one the reader opened and chose to
 * "Merge…". Its name is kept as the survivor's canonical name. */
export interface MergeSurvivor {
  readonly entityId: string;
  readonly name: string;
}

// Module-level, like useEntitySummary: one merge picker at a time, and a single revision
// counter every entity-derived panel can watch. The popup (which can be opened from an
// in-text highlight or the Story Guide) requests a merge; the picker — mounted once at the
// reader level — reads the survivor here and runs it.
const survivor = shallowRef<MergeSurvivor | null>(null);

// Bumped after a successful merge. The Story Guide, timeline, and relationship-graph panels
// add this to their reload triggers, so a merge refreshes any of them that's open without a
// reading-position change (their Dexie caches are cleared in the same step, so they refetch).
const mergeRevision = ref(0);

interface UseEntityMerge {
  readonly survivor: ShallowRef<MergeSurvivor | null>;
  readonly mergeRevision: Ref<number>;
  requestMerge: (target: MergeSurvivor) => void;
  cancelMerge: () => void;
  notifyMerged: () => void;
}

export function useEntityMerge(): UseEntityMerge {
  function requestMerge(target: MergeSurvivor): void {
    survivor.value = target;
  }

  function cancelMerge(): void {
    survivor.value = null;
  }

  function notifyMerged(): void {
    mergeRevision.value += 1;
  }

  return { survivor, mergeRevision, requestMerge, cancelMerge, notifyMerged };
}

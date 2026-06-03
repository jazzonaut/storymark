import { ref } from "vue";
import type { Ref } from "vue";
import { ApiError } from "@/api/api-error";
import type { CachedResult, CacheFallback } from "@/api/read-through-cache";
import { useLatestOnly } from "@/composables/use-latest-only";

/** The reactive load/refresh state every cached reader surface exposes: a skeleton
 * on first load, the cached copy under a spinner once the read-through cache
 * surfaces one mid-refresh, an error message when nothing usable loaded, and a
 * fallback reason when a refresh failed over a saved copy. */
export interface CachedLoadState {
  readonly isLoading: Ref<boolean>;
  /** A refresh is in flight over an already-shown cached copy (vs. `isLoading`,
   * which is a first load with nothing to show yet). */
  readonly isRefreshing: Ref<boolean>;
  readonly error: Ref<string | null>;
  /** Why the shown data is a saved copy (the live refresh failed), or null when
   * fresh; drives the refresh indicator's icon. */
  readonly fallback: Ref<CacheFallback | null>;
}

/** Handed to a {@link CachedLoad.run} body so each of its steps stays gated to the
 * load that started it — a superseded load must never touch state. */
export interface CachedLoadContext {
  /** False once a later `run` (or an `invalidate`) has begun. Check after every
   * await before committing values. */
  readonly isCurrent: () => boolean;
  /** Wraps a value-applying callback into an `onRefreshingFrom` handler: paints the
   * boundary-safe cached copy and flips the surface from skeleton to refreshing —
   * unless the load has been superseded, in which case nothing is painted. */
  readonly showCached: <T>(apply: (value: T) => void) => (value: T) => void;
  /** Adopts the first fallback reason among the results, so a surface built from
   * several fetches flags stale when any of them fell back. A no-op once the load
   * is superseded, so a slow earlier load can't restamp a newer one's indicator. */
  readonly commit: (...results: readonly CachedResult<unknown>[]) => void;
}

export interface CachedLoadOptions<T> {
  load: (ctx: CachedLoadContext) => Promise<T>;
  /** Translates a failure into the surface's error message; see
   * {@link describeLoadError} for the standard pair. */
  describeError: (caught: unknown) => string;
  /** Clears the surface's value refs after a failure (gated like the message). */
  onFailure?: () => void;
}

/** The load lifecycle shared by every cached reader surface (Story Guide, summary,
 * timeline, graph, entity popup). `run` owns the transitions and their invariants —
 * starting clears prior state, only the latest load may paint or settle, and a
 * failure is reported only when still current — in one place instead of one copy
 * per surface. The surface keeps its own value refs and supplies the body, the
 * error wording, and what to clear on failure; those genuinely differ. */
export interface CachedLoad extends CachedLoadState {
  /** Runs one guarded load; resolves to the body's result, or null on failure
   * (after `describeError`/`onFailure` have applied, if still current). */
  run: <T>(opts: CachedLoadOptions<T>) => Promise<T | null>;
  /** Supersedes any in-flight load and returns to idle (e.g. the popup closed), so
   * a pending response can't repopulate state afterwards. */
  invalidate: () => void;
}

/** The standard message pair for a surface's load failure: an {@link ApiError}
 * means the request reached a server, anything else means no response ever
 * arrived (slow link or offline). */
export function describeLoadError(failed: string, offline: string): (caught: unknown) => string {
  return (caught) => (caught instanceof ApiError ? failed : offline);
}

export function useCachedLoad(): CachedLoad {
  const isLoading = ref(false);
  const isRefreshing = ref(false);
  const error = ref<string | null>(null);
  const fallback = ref<CacheFallback | null>(null);
  // A slow superseded load must not overwrite newer state, so every transition after
  // an await is gated on the token claimed when its run began.
  const latest = useLatestOnly();

  function clear(): void {
    isLoading.value = false;
    isRefreshing.value = false;
    error.value = null;
    fallback.value = null;
  }

  async function run<T>(opts: CachedLoadOptions<T>): Promise<T | null> {
    const token = latest.begin();
    clear();
    isLoading.value = true;
    const isCurrent = (): boolean => latest.isCurrent(token);
    try {
      return await opts.load({
        isCurrent,
        showCached: (apply) => (value) => {
          if (isCurrent()) {
            apply(value);
            isLoading.value = false;
            isRefreshing.value = true;
          }
        },
        commit: (...results) => {
          if (!isCurrent()) {
            return;
          }
          fallback.value =
            results.map((result) => result.fallback).find((reason) => reason !== undefined) ?? null;
        },
      });
    } catch (caught) {
      if (isCurrent()) {
        error.value = opts.describeError(caught);
        opts.onFailure?.();
      }
      return null;
    } finally {
      if (isCurrent()) {
        isLoading.value = false;
        isRefreshing.value = false;
      }
    }
  }

  return {
    isLoading,
    isRefreshing,
    error,
    fallback,
    run,
    invalidate: () => {
      latest.begin();
      clear();
    },
  };
}

import type { Table } from "dexie";
import { ApiError } from "@/api/api-error";
import { fetchJson } from "@/api/http";

// Section-keyed reader endpoints (events, relationships, entities) all cache the
// same way: a Dexie row fresher than this TTL is served directly; otherwise the
// value is fetched and written back, and on a network failure a stale row is still
// returned so the timeline, graph, and Story Guide survive offline. That
// serve-stale-on-failure behaviour is the load-bearing part, so it lives here once.
// Those three share the identical wiring in {@link positionAwareCache} below.
const CACHE_TTL_MS = 5 * 60 * 1000;

/** Why a read-through cache served a stale copy instead of fresh data, so the
 * refresh indicator can show the right icon: an HTTP status means the request
 * reached a server (so the code is worth surfacing), anything else means the
 * response never arrived (offline / slow link). */
export type CacheFallback =
  | { readonly kind: "offline" }
  | { readonly kind: "http"; readonly status: number };

export interface CachedResult<T> {
  readonly value: T;
  /** Present only when `value` is a stale saved copy served because the live fetch
   * failed; says why, so the UI can tell an offline failure from an HTTP error (and
   * show the status code) rather than flattening both to one "offline" message.
   * Absent when `value` is fresh or a current cache hit. */
  readonly fallback?: CacheFallback;
}

function describeCacheFallback(error: unknown): CacheFallback {
  return error instanceof ApiError ? { kind: "http", status: error.status } : { kind: "offline" };
}

/** Spreads to a `version` tag only when one is known — under
 * `exactOptionalPropertyTypes` an unknown version must omit the key entirely, not
 * set it to undefined. Accepts both spellings of "unknown": `null` (a caller that
 * doesn't know the book's version) and `undefined` (an untagged cached row). */
export function versionTag(version: string | null | undefined): { version?: string } {
  return version === null || version === undefined ? {} : { version };
}

/** Read-through TTL cache. Serves a current cached value, otherwise fetches and
 * writes back, falling back to a stale value when the fetch fails. Throws (the
 * fetch's error) only when nothing usable is cached.
 *
 * Position-aware caches (timeline/graph/Story Guide) pass `boundary` — the reader's
 * current spoiler boundary (location offset) — and tag each cached row with the
 * boundary it was built at. A row built *ahead* of the reader is never served (that
 * would leak later-in-section content); a row from an *earlier* boundary is a safe
 * subset, so it is kept only as the offline fallback and never short-circuits a
 * refresh. The fast no-network path is taken solely on an exact-boundary fresh hit.
 *
 * `version` is the book's extraction version (its `updatedAt`): a row tagged with a
 * different version predates a reprocess, so it is never treated as current (it is
 * refetched), though it stays usable as the offline fallback. `version: null` means
 * the caller doesn't know the version and the check is skipped.
 *
 * `canCache` is false while the book is still processing: its derived data is
 * incomplete, so an empty/partial response must not be written back or short-circuit
 * a later read once the book finishes. The fetch still runs and a prior safe row is
 * still the offline fallback.
 *
 * A row with no `cachedAt` is gated by `version`/`boundary` alone, not by time (the
 * memory-snapshot cache, whose snapshots are stable for a given version): it never
 * expires by TTL and is served until its version no longer matches. */
export async function readThroughCache<T>(source: {
  boundary?: number;
  version?: string | null;
  canCache?: boolean;
  readCached: () => Promise<
    { value: T; cachedAt?: number; boundary?: number; version?: string } | undefined
  >;
  fetchFresh: () => Promise<T>;
  writeBack: (value: T) => Promise<unknown>;
  /** Invoked with the boundary-safe cached value when a network fetch is about to
   * run and such a value exists — so the UI can render it (with a refreshing
   * indicator) instead of a blank skeleton. Not called on the fast cached path (the
   * value is the result) nor when no safe row exists (an ahead-of-reader row is never
   * shown — a skeleton is correct there). */
  onRefreshingFrom?: ((value: T) => void) | undefined;
}): Promise<CachedResult<T>> {
  const cached = await source.readCached();
  const canCache = source.canCache ?? true;
  // delta > 0: the row is ahead of the reader (spoiler) → never serve.
  // delta < 0: the row is behind (a safe subset) → usable offline, but refresh first.
  // delta = 0: exact boundary, or a position-independent cache → serve when fresh.
  const delta =
    cached?.boundary !== undefined && source.boundary !== undefined
      ? cached.boundary - source.boundary
      : 0;
  // A null version means "don't gate on version"; otherwise the row is current only
  // when its tagged version matches (an untagged legacy row never counts as current).
  const versionMatches =
    source.version === undefined || source.version === null
      ? true
      : cached?.version === source.version;
  const safe = cached !== undefined && delta <= 0 ? cached : undefined;
  // A row without `cachedAt` is version/boundary-gated rather than time-gated, so it
  // never expires by TTL.
  const withinTtl =
    safe !== undefined &&
    (safe.cachedAt === undefined || Date.now() - safe.cachedAt < CACHE_TTL_MS);
  if (safe !== undefined && delta === 0 && versionMatches && canCache && withinTtl) {
    return { value: safe.value };
  }
  if (safe !== undefined) {
    source.onRefreshingFrom?.(safe.value);
  }
  try {
    const value = await source.fetchFresh();
    if (canCache) {
      await source.writeBack(value);
    }
    return { value };
  } catch (caught) {
    if (safe !== undefined) {
      return { value: safe.value, fallback: describeCacheFallback(caught) };
    }
    throw caught;
  }
}

/** The shared envelope of every position-aware cache row: its key
 * ([bookId+upToSection]), the `upToLocation` boundary it was built at, the `cachedAt`
 * TTL stamp, and the optional book `version`. */
interface PositionRow {
  readonly bookId: string;
  readonly upToSection: number;
  readonly upToLocation: number;
  readonly cachedAt: number;
  readonly version?: string;
}

/** The position-aware read-through shared by the timeline, graph, and Story Guide:
 * one Dexie store per surface, all keyed by [bookId+upToSection], tagged with the
 * reader's `upToLocation` boundary and the book `version`, and fetched from
 * `/api/books/{id}/{resource}?upToSection=…&upToLocation=…`. Wraps
 * {@link readThroughCache} with that shared wiring so each surface supplies only its
 * store and the one row field that differs — `pluck` to read it, `pack` to write it. */
export function positionAwareCache<TItem, TRow extends PositionRow>(opts: {
  bookId: string;
  upToSection: number;
  upToLocation: number;
  version: string | null;
  canCache: boolean;
  resource: string;
  errorMessage: string;
  table: Table<TRow, [string, number]>;
  pluck: (row: TRow) => readonly TItem[];
  pack: (items: readonly TItem[], base: PositionRow) => TRow;
  onRefreshingFrom?: ((items: readonly TItem[]) => void) | undefined;
}): Promise<CachedResult<readonly TItem[]>> {
  const { bookId, upToSection, upToLocation, version, canCache } = opts;
  return readThroughCache<readonly TItem[]>({
    boundary: upToLocation,
    version,
    canCache,
    onRefreshingFrom: opts.onRefreshingFrom,
    readCached: async () => {
      const row = await opts.table.get([bookId, upToSection]);
      return row
        ? {
            value: opts.pluck(row),
            cachedAt: row.cachedAt,
            boundary: row.upToLocation,
            ...versionTag(row.version),
          }
        : undefined;
    },
    fetchFresh: () =>
      fetchJson<readonly TItem[]>(
        `/api/books/${encodeURIComponent(bookId)}/${opts.resource}?upToSection=${String(upToSection)}&upToLocation=${String(upToLocation)}`,
        opts.errorMessage,
      ),
    writeBack: (items) =>
      opts.table.put(
        opts.pack(items, {
          bookId,
          upToSection,
          upToLocation,
          cachedAt: Date.now(),
          ...versionTag(version),
        }),
      ),
  });
}

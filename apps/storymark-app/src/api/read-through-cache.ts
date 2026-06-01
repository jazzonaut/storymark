// Section-keyed reader endpoints (events, relationships, entities) all cache the
// same way: a Dexie row fresher than this TTL is served directly; otherwise the
// value is fetched and written back, and on a network failure a stale row is still
// returned so the timeline, graph, and Story Guide survive offline. That
// serve-stale-on-failure behaviour is the load-bearing part, so it lives here once.
const CACHE_TTL_MS = 5 * 60 * 1000;

/** Read-through TTL cache. Serves a fresh cached value, otherwise fetches and
 * writes back, falling back to a stale value when the fetch fails. Throws (the
 * fetch's error) only when nothing is cached. */
export async function readThroughCache<T>(source: {
  readCached: () => Promise<{ value: T; cachedAt: number } | undefined>;
  fetchFresh: () => Promise<T>;
  writeBack: (value: T) => Promise<unknown>;
}): Promise<T> {
  const cached = await source.readCached();
  if (cached && Date.now() - cached.cachedAt < CACHE_TTL_MS) {
    return cached.value;
  }
  try {
    const value = await source.fetchFresh();
    await source.writeBack(value);
    return value;
  } catch (caught) {
    if (cached) {
      return cached.value;
    }
    throw caught;
  }
}

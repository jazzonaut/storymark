/** A monotonic guard for module-level async loads. `begin()` claims the next token
 * and makes every earlier token stale; `isCurrent(token)` is false once a later load
 * has begun, so a slow earlier fetch can't overwrite newer state. Create one per
 * surface (timeline, graph, Story Guide) and check the token after each await. */
export interface LatestOnly {
  begin: () => number;
  isCurrent: (token: number) => boolean;
}

export function useLatestOnly(): LatestOnly {
  let latest = 0;
  return {
    begin: () => (latest += 1),
    isCurrent: (token: number) => token === latest,
  };
}

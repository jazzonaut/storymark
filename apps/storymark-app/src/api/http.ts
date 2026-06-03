import { ApiError } from "@/api/api-error";

// A mobile connection can leave a fetch half-open with no response and no error,
// which would hang the caller indefinitely (a never-settling read loop, or an
// assistant/entity-summary spinner that never clears with no way to retry).
// Aborting after a bound turns that into a failure the caller can surface and
// retry. LLM-backed calls (Q&A, entity summary, selection hint) legitimately take
// longer than a plain query, so they get a wider bound.
export const DEFAULT_TIMEOUT_MS = 20_000;
export const LLM_TIMEOUT_MS = 60_000;

/** True when `caught` is {@link apiFetch}'s timeout firing: the request ran too
 * long (an LLM call on a slow model, a stalled mobile link), as opposed to never
 * reaching a server at all. Lets a caller word the two failures differently. */
export function isTimeout(caught: unknown): boolean {
  return caught instanceof DOMException && caught.name === "TimeoutError";
}

/** The single fetch primitive for the app: identical to `fetch` but with a
 * mandatory abort timeout so no request can hang forever. A timeout surfaces as
 * the standard `TimeoutError` (an `AbortError`-family `DOMException`) — see
 * {@link isTimeout}. A caller-supplied `init.signal` is honoured alongside the
 * timeout. */
export function apiFetch(
  path: string,
  init: RequestInit = {},
  timeoutMs: number = DEFAULT_TIMEOUT_MS,
): Promise<Response> {
  const timeout = AbortSignal.timeout(timeoutMs);
  return fetch(path, {
    ...init,
    signal: init.signal != null ? AbortSignal.any([init.signal, timeout]) : timeout,
  });
}

/** The happy-path JSON wrapper: fetches with a timeout and parses the body,
 * throwing {@link ApiError} (`<label> (<status>).`) on any non-2xx response.
 * Endpoints with 204/empty-body, void, or custom-error semantics call
 * {@link apiFetch} directly instead. */
export async function fetchJson<T>(
  path: string,
  label: string,
  init: RequestInit = {},
  timeoutMs: number = DEFAULT_TIMEOUT_MS,
): Promise<T> {
  const response = await apiFetch(path, init, timeoutMs);
  if (!response.ok) {
    throw new ApiError(response.status, `${label} (${response.status}).`);
  }
  return (await response.json()) as T;
}

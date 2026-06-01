import { ApiError } from "@/api/api-error";

// A mobile connection can leave a fetch half-open with no response and no error,
// which would hang the caller indefinitely (a never-settling read loop, or an
// assistant/entity-summary spinner that never clears with no way to retry).
// Aborting after a bound turns that into a failure the caller can surface and
// retry. LLM-backed calls (Q&A, entity summary, selection hint) legitimately take
// longer than a plain query, so they get a wider bound.
export const DEFAULT_TIMEOUT_MS = 20_000;
export const LLM_TIMEOUT_MS = 60_000;

/** The single fetch primitive for the app: identical to `fetch` but with a
 * mandatory abort timeout so no request can hang forever. A timeout surfaces as
 * the standard `TimeoutError` (an `AbortError`-family `DOMException`), which the
 * api wrappers treat the same as any other network failure. */
export function apiFetch(
  path: string,
  init: RequestInit = {},
  timeoutMs: number = DEFAULT_TIMEOUT_MS,
): Promise<Response> {
  return fetch(path, { ...init, signal: AbortSignal.timeout(timeoutMs) });
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

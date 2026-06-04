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

let authExpiredHandler: (() => void) | null = null;

/** Registered once by `use-auth` (a callback rather than an import so this
 * module stays dependency-free — `use-auth` imports the API layer). */
export function setAuthExpiredHandler(handler: () => void): void {
  authExpiredHandler = handler;
}

/** Raises the auth-expired signal directly — for authed content the browser
 * loads itself (`<img>` covers and inline book images), whose 401s never pass
 * through {@link apiFetch}. `use-auth` probes the session after such a failure
 * and calls this when the session is gone. */
export function raiseAuthExpired(): void {
  authExpiredHandler?.();
}

/** A 401 means the session cookie expired or was revoked — admin endpoints
 * included, since they ride the same session (ADR-0024). `/api/auth/*` is
 * excluded (a login 401 is a credential failure and the `me` probe answers 401
 * for "anonymous"). */
function notifyIfAuthExpired(path: string, response: Response): void {
  if (response.status === 401 && !path.startsWith("/api/auth/")) {
    raiseAuthExpired();
  }
}

/** The single fetch primitive for the app: identical to `fetch` but with a
 * mandatory abort timeout so no request can hang forever. A timeout surfaces as
 * the standard `TimeoutError` (an `AbortError`-family `DOMException`) — see
 * {@link isTimeout}. A caller-supplied `init.signal` is honoured alongside the
 * timeout. */
export async function apiFetch(
  path: string,
  init: RequestInit = {},
  timeoutMs: number = DEFAULT_TIMEOUT_MS,
): Promise<Response> {
  const timeout = AbortSignal.timeout(timeoutMs);
  const response = await fetch(path, {
    ...init,
    signal: init.signal != null ? AbortSignal.any([init.signal, timeout]) : timeout,
  });
  notifyIfAuthExpired(path, response);
  return response;
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

import { ref } from "vue";
import type { Ref } from "vue";
import type { AuthUser } from "@storymark/shared-types";
import { authApi } from "@/api/auth-api";
import { raiseAuthExpired } from "@/api/http";
import { useOfflineSync } from "@/composables/use-offline-sync";
import { forgetLastUser, hasLastKnownUser, switchCachePartition } from "@/db/storymark-db";

/** `unknown` covers both "not probed yet" and "probe unreachable" (offline) —
 * the router guard lets it through so cached reading keeps working without a
 * server roundtrip (ADR-0006); a live 401 later raises the auth-expired
 * signal and demotes the session to `anonymous`. */
export type SessionState = "unknown" | "authenticated" | "anonymous";

/** `switched-user` means the cache partition moved away from a different
 * account: module-level state still holds that account's data in memory, so
 * the caller must restart the app shell with a full navigation, not a router
 * transition. */
export type LoginResult = "invalid" | "signed-in" | "switched-user";

const user = ref<AuthUser | null>(null);
const sessionState = ref<SessionState>("unknown");
const { requestFlush } = useOfflineSync();

interface UseAuth {
  readonly user: Ref<AuthUser | null>;
  readonly sessionState: Ref<SessionState>;
  ensureSession: () => Promise<SessionState>;
  login: (userName: string, password: string) => Promise<LoginResult>;
  logout: () => Promise<void>;
  markSessionLost: () => void;
}

async function ensureSession(): Promise<SessionState> {
  if (sessionState.value !== "unknown") {
    return sessionState.value;
  }

  let me: AuthUser | null;
  try {
    me = await authApi.me();
  } catch {
    // Unreachable or erroring server: with a last-known account the device
    // still has that partition to serve, so stay unknown and let cached
    // reading work (ADR-0006) — the next navigation probes again. With none
    // (a fresh device, or after a sign-out: see forgetLastUser) there is
    // nothing to serve, so the sign-in screen beats an empty library.
    return hasLastKnownUser() ? "unknown" : "anonymous";
  }

  user.value = me;
  sessionState.value = me === null ? "anonymous" : "authenticated";
  if (me !== null) {
    // Align the partition with the cookie's identity. This runs inside the
    // router guard, before the first route renders, so no module state holds
    // another partition's data yet — no restart needed here, unlike login().
    switchCachePartition(me.userId);
  }
  return sessionState.value;
}

async function login(userName: string, password: string): Promise<LoginResult> {
  const me = await authApi.login(userName, password);
  if (me === null) {
    return "invalid";
  }
  user.value = me;
  sessionState.value = "authenticated";
  const switched = switchCachePartition(me.userId);
  // A session that expired with queued progress/bookmark writes left them in
  // this account's partition; the fresh cookie is what lets them through now.
  // On a switched-user login the previous account's queue stays in its own
  // partition — it must not be sent under this cookie (the server attributes
  // writes to the session's user) and drains when that account next signs in.
  requestFlush();
  return switched ? "switched-user" : "signed-in";
}

// Leaves the partition data untouched (cached content stays warm for a
// re-login) but forgets the last-known user: ADR-0023's offline tolerance
// covers a cookie *expiring* under the same reader, not an explicit sign-out —
// after one, an offline cold start must not serve the account's library to
// whoever holds the device. Local demotion runs in `finally` so a failing
// logout call can't leave the session stuck on "authenticated".
async function logout(): Promise<void> {
  try {
    await authApi.logout();
  } finally {
    markSessionLost();
    forgetLastUser();
  }
}

function markSessionLost(): void {
  user.value = null;
  sessionState.value = "anonymous";
}

let imageAuthProbe: Promise<void> | null = null;

async function probeSessionAfterImageError(): Promise<void> {
  try {
    if ((await authApi.me()) === null) {
      raiseAuthExpired();
    }
  } catch {
    // Unreachable server: an offline image failure, not an auth problem.
  }
}

// Covers and inline book images load through `<img src>` (and the service
// worker), not apiFetch, so an expired session surfaces only as broken images —
// http.ts's 401 handler never sees them. A failed /api/ image while
// "authenticated" triggers one session probe; a dead session raises the same
// expired signal as an apiFetch 401. Capture phase is required: error events on
// media elements don't bubble. Registered once at module load, like
// use-offline-sync's window listeners.
document.addEventListener(
  "error",
  (event) => {
    if (
      sessionState.value !== "authenticated" ||
      !(event.target instanceof HTMLImageElement) ||
      !new URL(event.target.src, window.location.origin).pathname.startsWith("/api/")
    ) {
      return;
    }
    imageAuthProbe ??= probeSessionAfterImageError().finally(() => {
      imageAuthProbe = null;
    });
  },
  true,
);

export function useAuth(): UseAuth {
  return { user, sessionState, ensureSession, login, logout, markSessionLost };
}

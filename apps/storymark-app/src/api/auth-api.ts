import type { AuthUser } from "@storymark/shared-types";
import { ApiError } from "@/api/api-error";
import { apiFetch } from "@/api/http";

/** Shared by login/me: a 401 is "no session" (`null`), anything else non-2xx
 * throws {@link ApiError}. */
async function userOrNull(response: Response, label: string): Promise<AuthUser | null> {
  if (response.status === 401) {
    return null;
  }
  if (!response.ok) {
    throw new ApiError(response.status, `${label} (${response.status}).`);
  }
  return (await response.json()) as AuthUser;
}

export const authApi = {
  /** Signs in. `null` means rejected credentials — the server keeps bad
   * password, unknown user, and lockout indistinguishable. Throws
   * {@link ApiError} on other failures (incl. offline). */
  login: async (userName: string, password: string): Promise<AuthUser | null> =>
    userOrNull(
      await apiFetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ userName, password }),
      }),
      "Sign-in failed",
    ),

  /** The session behind the cookie, or `null` when not signed in. */
  me: async (): Promise<AuthUser | null> =>
    userOrNull(await apiFetch("/api/auth/me"), "Session check failed"),

  /** Idempotent — a 401 just means the session was already gone. */
  logout: async (): Promise<void> => {
    const response = await apiFetch("/api/auth/logout", { method: "POST" });
    if (!response.ok && response.status !== 401) {
      throw new ApiError(response.status, `Sign-out failed (${response.status}).`);
    }
  },
} as const;

import type { PatchSettingBody, PromptItem, SettingItem } from "@/api/admin-types";

/** Thrown on any non-2xx admin response. <see cref="status"/> drives 401 handling. */
export class AdminApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "AdminApiError";
    this.status = status;
  }
}

async function request<T>(token: string, path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api/admin/${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    throw new AdminApiError(response.status, `Admin request failed (${response.status}).`);
  }

  return (await response.json()) as T;
}

export const adminApi = {
  listSettings: (token: string): Promise<readonly SettingItem[]> => request(token, "settings"),

  patchSetting: (token: string, body: PatchSettingBody): Promise<SettingItem> =>
    request(token, "settings", { method: "PATCH", body: JSON.stringify(body) }),

  listPrompts: (token: string): Promise<readonly PromptItem[]> => request(token, "prompts"),

  patchPrompt: (token: string, key: string, text: string): Promise<PromptItem> =>
    request(token, `prompts/${encodeURIComponent(key)}`, {
      method: "PATCH",
      body: JSON.stringify({ text }),
    }),
} as const;

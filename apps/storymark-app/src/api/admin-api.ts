import { apiFetch } from "@/api/http";
import type {
  ApplySwapResult,
  BackfillSectionTitlesResult,
  CostSummary,
  CreateUserBody,
  EmbeddingImpactPreview,
  LlmCallItem,
  ModelItem,
  ModelUpsertBody,
  PatchSettingBody,
  PromptItem,
  ProviderItem,
  ProviderUpsertBody,
  RecoverStuckBooksResult,
  ReprocessBookResult,
  SettingItem,
  UserAdminItem,
} from "@/api/admin-types";

/** Thrown on any non-2xx admin response. `status` drives 401 handling; `body` is
 * the parsed JSON error payload (or null), so endpoints that reject with a
 * structured reason — e.g. reprocess answering 409 with the book's mid-pipeline
 * status — can surface it instead of a generic failure line. */
export class AdminApiError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(status: number, message: string, body: unknown) {
    super(message);
    this.name = "AdminApiError";
    this.status = status;
    this.body = body;
  }
}

// Authentication is the session cookie (ADR-0024), sent by the browser on its
// own — admin requests carry no credential header.
async function send(path: string, init?: RequestInit): Promise<Response> {
  const response = await apiFetch(`/api/admin/${path}`, {
    ...init,
    headers: { "Content-Type": "application/json" },
  });

  if (!response.ok) {
    const body: unknown = await response.json().catch(() => null);
    throw new AdminApiError(response.status, `Admin request failed (${response.status}).`, body);
  }

  return response;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await send(path, init);
  return (await response.json()) as T;
}

async function requestVoid(path: string, init: RequestInit): Promise<void> {
  await send(path, init);
}

export const adminApi = {
  listSettings: (): Promise<readonly SettingItem[]> => request("settings"),

  patchSetting: (body: PatchSettingBody): Promise<SettingItem> =>
    request("settings", { method: "PATCH", body: JSON.stringify(body) }),

  listPrompts: (): Promise<readonly PromptItem[]> => request("prompts"),

  patchPrompt: (key: string, text: string): Promise<PromptItem> =>
    request(`prompts/${encodeURIComponent(key)}`, {
      method: "PATCH",
      body: JSON.stringify({ text }),
    }),

  listLlmCalls: (limit: number): Promise<readonly LlmCallItem[]> =>
    request(`llm-calls?limit=${limit}`),

  getCostSummary: (): Promise<CostSummary> => request("llm-calls/cost-summary"),

  getEmbeddingImpact: (): Promise<EmbeddingImpactPreview> => request("embedding/impact-preview"),

  applyEmbeddingSwap: (modelId: string): Promise<ApplySwapResult> =>
    request("embedding/apply-swap", {
      method: "POST",
      body: JSON.stringify({ modelId }),
    }),

  backfillSectionTitles: (): Promise<BackfillSectionTitlesResult> =>
    request("books/backfill-section-titles", { method: "POST" }),

  recoverStuckBooks: (staleMinutes: number): Promise<RecoverStuckBooksResult> =>
    request("books/recover-stuck", {
      method: "POST",
      body: JSON.stringify({ staleMinutes }),
    }),

  reprocessBook: (bookId: string): Promise<ReprocessBookResult> =>
    request(`books/reprocess/${encodeURIComponent(bookId)}`, { method: "POST" }),

  listProviders: (): Promise<readonly ProviderItem[]> => request("models/providers"),

  createProvider: (body: ProviderUpsertBody): Promise<ProviderItem> =>
    request("models/providers", { method: "POST", body: JSON.stringify(body) }),

  updateProvider: (id: string, body: ProviderUpsertBody): Promise<ProviderItem> =>
    request(`models/providers/${id}`, { method: "PATCH", body: JSON.stringify(body) }),

  deleteProvider: (id: string): Promise<void> =>
    requestVoid(`models/providers/${id}`, { method: "DELETE" }),

  listModels: (): Promise<readonly ModelItem[]> => request("models"),

  createModel: (body: ModelUpsertBody): Promise<ModelItem> =>
    request("models", { method: "POST", body: JSON.stringify(body) }),

  updateModel: (id: string, body: ModelUpsertBody): Promise<ModelItem> =>
    request(`models/${id}`, { method: "PATCH", body: JSON.stringify(body) }),

  deleteModel: (id: string): Promise<void> => requestVoid(`models/${id}`, { method: "DELETE" }),

  listUsers: (): Promise<readonly UserAdminItem[]> => request("users"),

  createUser: (body: CreateUserBody): Promise<UserAdminItem> =>
    request("users", { method: "POST", body: JSON.stringify(body) }),

  /** Set-or-reset; the endpoint also clears any lockout (the recovery path). */
  setUserPassword: (id: string, password: string): Promise<void> =>
    requestVoid(`users/${encodeURIComponent(id)}/password`, {
      method: "POST",
      body: JSON.stringify({ password }),
    }),

  /** Grants or revokes admin. The server rotates the account's security stamp,
   * so the affected user's sessions end and the change applies at next sign-in. */
  setUserAdmin: (id: string, isAdmin: boolean): Promise<void> =>
    requestVoid(`users/${encodeURIComponent(id)}/admin`, {
      method: "POST",
      body: JSON.stringify({ isAdmin }),
    }),
} as const;

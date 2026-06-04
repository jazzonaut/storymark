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

async function send(token: string, path: string, init?: RequestInit): Promise<Response> {
  const response = await apiFetch(`/api/admin/${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    const body: unknown = await response.json().catch(() => null);
    throw new AdminApiError(response.status, `Admin request failed (${response.status}).`, body);
  }

  return response;
}

async function request<T>(token: string, path: string, init?: RequestInit): Promise<T> {
  const response = await send(token, path, init);
  return (await response.json()) as T;
}

async function requestVoid(token: string, path: string, init: RequestInit): Promise<void> {
  await send(token, path, init);
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

  listLlmCalls: (token: string, limit: number): Promise<readonly LlmCallItem[]> =>
    request(token, `llm-calls?limit=${limit}`),

  getCostSummary: (token: string): Promise<CostSummary> => request(token, "llm-calls/cost-summary"),

  getEmbeddingImpact: (token: string): Promise<EmbeddingImpactPreview> =>
    request(token, "embedding/impact-preview"),

  applyEmbeddingSwap: (token: string, modelId: string): Promise<ApplySwapResult> =>
    request(token, "embedding/apply-swap", {
      method: "POST",
      body: JSON.stringify({ modelId }),
    }),

  backfillSectionTitles: (token: string): Promise<BackfillSectionTitlesResult> =>
    request(token, "books/backfill-section-titles", { method: "POST" }),

  recoverStuckBooks: (token: string, staleMinutes: number): Promise<RecoverStuckBooksResult> =>
    request(token, "books/recover-stuck", {
      method: "POST",
      body: JSON.stringify({ staleMinutes }),
    }),

  reprocessBook: (token: string, bookId: string): Promise<ReprocessBookResult> =>
    request(token, `books/reprocess/${encodeURIComponent(bookId)}`, { method: "POST" }),

  listProviders: (token: string): Promise<readonly ProviderItem[]> =>
    request(token, "models/providers"),

  createProvider: (token: string, body: ProviderUpsertBody): Promise<ProviderItem> =>
    request(token, "models/providers", { method: "POST", body: JSON.stringify(body) }),

  updateProvider: (token: string, id: string, body: ProviderUpsertBody): Promise<ProviderItem> =>
    request(token, `models/providers/${id}`, { method: "PATCH", body: JSON.stringify(body) }),

  deleteProvider: (token: string, id: string): Promise<void> =>
    requestVoid(token, `models/providers/${id}`, { method: "DELETE" }),

  listModels: (token: string): Promise<readonly ModelItem[]> => request(token, "models"),

  createModel: (token: string, body: ModelUpsertBody): Promise<ModelItem> =>
    request(token, "models", { method: "POST", body: JSON.stringify(body) }),

  updateModel: (token: string, id: string, body: ModelUpsertBody): Promise<ModelItem> =>
    request(token, `models/${id}`, { method: "PATCH", body: JSON.stringify(body) }),

  deleteModel: (token: string, id: string): Promise<void> =>
    requestVoid(token, `models/${id}`, { method: "DELETE" }),

  listUsers: (token: string): Promise<readonly UserAdminItem[]> => request(token, "users"),

  createUser: (token: string, body: CreateUserBody): Promise<UserAdminItem> =>
    request(token, "users", { method: "POST", body: JSON.stringify(body) }),

  /** Set-or-reset; the endpoint also clears any lockout (the recovery path). */
  setUserPassword: (token: string, id: string, password: string): Promise<void> =>
    requestVoid(token, `users/${encodeURIComponent(id)}/password`, {
      method: "POST",
      body: JSON.stringify({ password }),
    }),
} as const;

import type {
  ApplySwapResult,
  CostSummary,
  EmbeddingImpactPreview,
  LlmCallItem,
  ModelItem,
  ModelUpsertBody,
  PatchSettingBody,
  PromptItem,
  ProviderItem,
  ProviderUpsertBody,
  SettingItem,
} from "@/api/admin-types";

/** Thrown on any non-2xx admin response. <see cref="status"/> drives 401 handling. */
export class AdminApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "AdminApiError";
    this.status = status;
  }
}

async function send(token: string, path: string, init?: RequestInit): Promise<Response> {
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
} as const;

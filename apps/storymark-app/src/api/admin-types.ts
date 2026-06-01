// Contracts for the admin API (mirrors the backend DTOs in
// apps/storymark-api/Endpoints/Admin). Kept app-local: not shared with anything.

export type SettingValueType = "String" | "Int" | "Bool" | "Json";

export interface SettingItem {
  readonly key: string;
  readonly valueType: SettingValueType;
  readonly isSecret: boolean;
  /** JSON text of the value; the literal `"••••"` when the setting is secret. */
  readonly value: string;
}

export interface PromptItem {
  readonly key: string;
  readonly text: string;
}

export interface PatchSettingBody {
  readonly key: string;
  /** The value as its natural JSON shape; serialised as-is into the request. */
  readonly value: string | number | boolean | object;
  readonly type: SettingValueType;
}

export interface LlmCallItem {
  readonly id: string;
  readonly task: string;
  readonly provider: string;
  readonly model: string;
  readonly prompt: string;
  readonly response: string | null;
  readonly tokensIn: number | null;
  readonly tokensOut: number | null;
  readonly latencyMs: number | null;
  readonly costEstimate: number | null;
  readonly bookId: string | null;
  readonly sectionIndex: number | null;
  readonly usedFutureContent: boolean;
  readonly succeeded: boolean;
  readonly error: string | null;
  readonly createdAt: string;
}

export interface TaskCostItem {
  readonly task: string;
  readonly callCount: number;
  readonly tokensIn: number | null;
  readonly tokensOut: number | null;
  /** Null when no call in the task has a configured cost rate (e.g. local Ollama). */
  readonly costEstimate: number | null;
}

export interface CostSummary {
  readonly windowDays: number;
  readonly callCount: number;
  readonly tokensIn: number | null;
  readonly tokensOut: number | null;
  readonly costEstimate: number | null;
  readonly tasks: readonly TaskCostItem[];
}

export type LlmProviderKind = "ollama" | "openai_compatible";

export interface ProviderItem {
  readonly id: string;
  readonly name: string;
  readonly kind: LlmProviderKind;
  readonly baseUrl: string;
  /** Whether a key is stored; the key itself is never returned (write-only). */
  readonly hasApiKey: boolean;
}

export interface ProviderUpsertBody {
  readonly name: string;
  readonly kind: LlmProviderKind;
  readonly baseUrl: string;
  /** A new key, or null to leave the stored one unchanged. */
  readonly apiKey: string | null;
}

export interface ModelItem {
  readonly id: string;
  readonly label: string;
  readonly providerId: string;
  readonly modelName: string;
  readonly inputPerMillion: number | null;
  readonly outputPerMillion: number | null;
}

export interface ModelUpsertBody {
  readonly label: string;
  readonly providerId: string;
  readonly modelName: string;
  readonly inputPerMillion: number | null;
  readonly outputPerMillion: number | null;
}

export interface EmbeddingImpactPreview {
  readonly booksAffected: number;
  readonly chunkCount: number;
  readonly estimatedSeconds: number;
}

export interface ApplySwapResult {
  readonly modelId: string;
  readonly booksQueued: number;
}

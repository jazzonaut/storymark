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

export interface EmbeddingImpactPreview {
  readonly booksAffected: number;
  readonly chunkCount: number;
  readonly estimatedSeconds: number;
}

export interface ApplySwapResult {
  readonly modelId: string;
  readonly booksQueued: number;
}

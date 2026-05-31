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

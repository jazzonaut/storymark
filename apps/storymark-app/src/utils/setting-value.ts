// Admin setting values arrive as raw strings with the typed value (String,
// Bool, Json, …) JSON-encoded inside. Decoding lives here so every admin view
// agrees on the wire format.

/** Changing this setting re-embeds every book, so views intercept edits to it
 * with a confirm dialog (EmbeddingSwapDialog) instead of the normal save. */
export const EMBEDDING_MODEL_KEY = "llm.embedding.modelId";

/** The decoded value, or undefined when the raw string isn't valid JSON. */
export function parseSettingJson(value: string): unknown {
  try {
    return JSON.parse(value);
  } catch {
    return undefined;
  }
}

/** The decoded string, or the raw value when it isn't a JSON-encoded string. */
export function parseSettingString(value: string): string {
  const result = parseSettingJson(value);
  return typeof result === "string" ? result : value;
}

export function parseSettingBool(value: string): boolean {
  return parseSettingJson(value) === true;
}

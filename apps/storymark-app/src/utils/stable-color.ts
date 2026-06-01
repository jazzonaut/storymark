// Mid-saturation hues that read on light, sepia, and dark themes.
const PALETTE = [
  "#3b82f6",
  "#ef4444",
  "#10b981",
  "#f59e0b",
  "#8b5cf6",
  "#ec4899",
  "#14b8a6",
  "#f97316",
] as const;

/** Maps an arbitrary key to a stable palette colour, so the same key is always the
 * same hue across renders. Shared by the timeline (event type) and the relationship
 * graph (entity type). */
export function stableColor(key: string): string {
  let hash = 0;
  for (const char of key) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  }
  return PALETTE[hash % PALETTE.length] ?? PALETTE[0];
}

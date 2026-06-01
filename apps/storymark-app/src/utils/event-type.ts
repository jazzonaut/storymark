// The extraction model sometimes returns an event type as an option list
// ("betrayal | enmity") rather than a single label; take the first segment.
function normalize(type: string | null): string {
  const first = (type ?? "").split("|")[0]?.trim().toLowerCase() ?? "";
  return first.length > 0 ? first : "event";
}

/** A human label for an event type, e.g. `"betrayal | enmity"` → `"Betrayal"`. */
export function eventTypeLabel(type: string | null): string {
  const value = normalize(type);
  return value.charAt(0).toUpperCase() + value.slice(1);
}

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

/** A stable colour for an event type, so the same type is always the same hue. */
export function eventTypeColor(type: string | null): string {
  const value = normalize(type);
  let hash = 0;
  for (const char of value) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  }
  return PALETTE[hash % PALETTE.length] ?? PALETTE[0];
}

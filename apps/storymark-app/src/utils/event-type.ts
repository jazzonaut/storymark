import { stableColor } from "@/utils/stable-color";

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

/** A stable colour for an event type, so the same type is always the same hue. */
export function eventTypeColor(type: string | null): string {
  return stableColor(normalize(type));
}

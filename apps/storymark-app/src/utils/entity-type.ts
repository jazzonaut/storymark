import { stableColor } from "@/utils/stable-color";

/** A stable colour for an entity type (Person, Place, …), so the same type is
 * always the same hue across the relationship graph. Unknown/blank types fold to a
 * single "unknown" hue. */
export function entityTypeColor(type: string): string {
  const value = type.trim().toLowerCase();
  return stableColor(value.length > 0 ? value : "unknown");
}

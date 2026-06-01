import { stableColor } from "@/utils/stable-color";

/** A stable colour for an entity type (Person, Place, …), so the same type is
 * always the same hue across the relationship graph. Unknown/blank types fold to a
 * single "unknown" hue. */
export function entityTypeColor(type: string): string {
  const value = type.trim().toLowerCase();
  return stableColor(value.length > 0 ? value : "unknown");
}

// Friendly plural labels for the Story Guide type filter, keyed by the backend
// EntityType names (Domain/Books/EntityType.cs). An unrecognised type falls back
// to its raw name, so a type added server-side still filters correctly.
const PLURAL_LABELS: Record<string, string> = {
  Person: "People",
  Place: "Places",
  Organization: "Organizations",
  Object: "Things",
  Event: "Events",
  Concept: "Concepts",
  Creature: "Creatures",
  Title: "Titles",
  Unknown: "Other",
};

// The order the backend declares the enum in, so filter chips read in a stable,
// sensible sequence rather than whichever type happened to appear first.
const TYPE_ORDER: readonly string[] = [
  "Person",
  "Place",
  "Organization",
  "Object",
  "Event",
  "Concept",
  "Creature",
  "Title",
  "Unknown",
];

/** A plural group label for an entity type, e.g. `"Person"` → `"People"`. */
export function entityTypePlural(type: string): string {
  return PLURAL_LABELS[type] ?? type;
}

/** Sort key placing entity types in the backend's declared order; unknown last. */
export function entityTypeOrder(type: string): number {
  const index = TYPE_ORDER.indexOf(type);
  return index === -1 ? TYPE_ORDER.length : index;
}

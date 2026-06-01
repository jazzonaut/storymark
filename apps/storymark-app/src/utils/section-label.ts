import type { SectionSummary } from "@storymark/shared-types";

// A section's display name, falling back to "Section N" when the manifest entry
// has no title (titles are optional in SectionSummary).
export function sectionLabel(
  manifest: readonly SectionSummary[],
  sectionIndex: number,
): string {
  const title = manifest.find((s) => s.sectionIndex === sectionIndex)?.title;
  return title ?? `Section ${String(sectionIndex)}`;
}

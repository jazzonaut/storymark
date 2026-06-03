import { computed, ref, shallowRef } from "vue";
import type { ComputedRef, Ref, ShallowRef } from "vue";
import type { Section, SectionSummary } from "@storymark/shared-types";
import { readerApi } from "@/api/reader-api";
import { useBooks } from "@/composables/use-books";

/** Where to land when a section first appears: 0 (start) when moving forward or
 * opening, the last paragraph (end) when paging backward, a specific paragraph
 * index when resuming a saved position, or the paragraph covering a character
 * location (jumping to a Q&A source, whose paragraph is only known post-load). */
type SectionEntry = "start" | "end" | number | { readonly location: number };

/** The paragraph whose half-open range covers `location`, else the first
 * paragraph starting at/after it, else the section end. The end fallback matters:
 * the server anchors an event whose evidence quote it couldn't find to the
 * section's end (LocationStart = text length), past every paragraph's half-open
 * range — falling back to 0 would turn "end of section" into "start of section". */
function paragraphIndexForLocation(section: Section, location: number): number {
  const containing = section.paragraphs.find(
    (p) => p.locationStart <= location && location < p.locationEnd,
  );
  if (containing) {
    return containing.paragraphIndex;
  }
  const after = section.paragraphs.find((p) => p.locationStart >= location);
  return after?.paragraphIndex ?? section.paragraphs.at(-1)?.paragraphIndex ?? 0;
}

/** A position to open a book at (a resumed reading position). */
export interface InitialPosition {
  readonly sectionIndex: number;
  readonly paragraphIndex: number;
  readonly pageIndex?: number;
}

const bookId = ref<string | null>(null);
const manifest = shallowRef<readonly SectionSummary[]>([]);
const currentPosition = ref(0);
const currentSection = shallowRef<Section | null>(null);
const currentParagraphIndex = ref(0);
const entryParagraph = ref(0);
const entryPage = ref<number | null>(null);
// Bumped on every navigation (open, goTo, next/prev). A same-section jump (e.g.
// a bookmark a few pages back) swaps the section payload without a remount —
// exactly like an entity-merge refresh — so the reader needs this to tell "land
// on the new entry point" apart from "stay where you are".
const navigationRevision = ref(0);
const isLoadingSection = ref(false);

interface UseReader {
  readonly manifest: ShallowRef<readonly SectionSummary[]>;
  readonly currentSection: ShallowRef<Section | null>;
  readonly currentPosition: Ref<number>;
  readonly currentParagraphIndex: Ref<number>;
  readonly entryParagraph: Ref<number>;
  readonly entryPage: Ref<number | null>;
  readonly navigationRevision: Ref<number>;
  readonly isLoadingSection: Ref<boolean>;
  readonly sectionCount: ComputedRef<number>;
  readonly currentSectionTitle: ComputedRef<string | null>;
  readonly currentSectionIndex: ComputedRef<number | null>;
  readonly hasNextSection: ComputedRef<boolean>;
  readonly hasPrevSection: ComputedRef<boolean>;
  open: (id: string, initial?: InitialPosition) => Promise<readonly SectionSummary[]>;
  goTo: (sectionIndex: number, paragraphIndex: number) => Promise<void>;
  goToLocation: (sectionIndex: number, location: number) => Promise<number | null>;
  nextSection: () => Promise<boolean>;
  prevSection: () => Promise<boolean>;
  setParagraph: (paragraphIndex: number) => void;
  reloadCurrentSection: () => Promise<void>;
}

const sectionCount = computed<number>(() => manifest.value.length);
const currentSectionTitle = computed<string | null>(
  () => manifest.value[currentPosition.value]?.title ?? null,
);
const currentSectionIndex = computed<number | null>(
  () => manifest.value[currentPosition.value]?.sectionIndex ?? null,
);
const hasNextSection = computed<boolean>(() => currentPosition.value < manifest.value.length - 1);
const hasPrevSection = computed<boolean>(() => currentPosition.value > 0);

export function useReader(): UseReader {
  const { cacheParams } = useBooks();

  async function loadPosition(
    position: number,
    entry: SectionEntry,
    pageIndex: number | null = null,
  ): Promise<void> {
    const summary = manifest.value[position];
    const id = bookId.value;
    if (!summary || id === null) {
      return;
    }
    isLoadingSection.value = true;
    try {
      const { version, canCache } = cacheParams(id);
      const section = await readerApi.getSection(id, summary.sectionIndex, version, canCache);
      const lastIndex = Math.max(section.paragraphs.length - 1, 0);
      const requested =
        entry === "start"
          ? 0
          : entry === "end"
            ? lastIndex
            : typeof entry === "number"
              ? entry
              : paragraphIndexForLocation(section, entry.location);
      const landing = Math.min(Math.max(requested, 0), lastIndex);
      currentPosition.value = position;
      entryParagraph.value = landing;
      entryPage.value = pageIndex;
      currentParagraphIndex.value = landing;
      currentSection.value = section;
      navigationRevision.value += 1;
    } finally {
      isLoadingSection.value = false;
    }
  }

  async function open(id: string, initial?: InitialPosition): Promise<readonly SectionSummary[]> {
    bookId.value = id;
    currentSection.value = null;
    currentPosition.value = 0;
    currentParagraphIndex.value = 0;
    manifest.value = await readerApi.getManifest(id, cacheParams(id).version);

    const startPos = initial
      ? manifest.value.findIndex((s) => s.sectionIndex === initial.sectionIndex)
      : -1;
    if (initial && startPos >= 0) {
      await loadPosition(startPos, initial.paragraphIndex, initial.pageIndex ?? null);
    } else {
      await loadPosition(0, "start");
    }
    return manifest.value;
  }

  // Jump to a position in the already-open book (e.g. tapping a bookmark).
  async function goTo(sectionIndex: number, paragraphIndex: number): Promise<void> {
    if (isLoadingSection.value) {
      return;
    }
    const position = manifest.value.findIndex((s) => s.sectionIndex === sectionIndex);
    if (position >= 0) {
      await loadPosition(position, paragraphIndex);
    }
  }

  // Jump to the paragraph covering a character location (a tapped Q&A source).
  // Returns the resolved paragraph index so the caller can highlight it.
  async function goToLocation(sectionIndex: number, location: number): Promise<number | null> {
    if (isLoadingSection.value) {
      return null;
    }
    const position = manifest.value.findIndex((s) => s.sectionIndex === sectionIndex);
    if (position < 0) {
      return null;
    }
    await loadPosition(position, { location });
    return currentParagraphIndex.value;
  }

  async function nextSection(): Promise<boolean> {
    // Ignore a fresh request while one is mid-flight: a fast double tap-edge
    // would otherwise read a stale currentPosition and double-advance / race.
    if (isLoadingSection.value || !hasNextSection.value) {
      return false;
    }
    await loadPosition(currentPosition.value + 1, "start");
    return true;
  }

  async function prevSection(): Promise<boolean> {
    if (isLoadingSection.value || !hasPrevSection.value) {
      return false;
    }
    await loadPosition(currentPosition.value - 1, "end");
    return true;
  }

  function setParagraph(paragraphIndex: number): void {
    currentParagraphIndex.value = paragraphIndex;
  }

  // Re-fetch the open section's content in place (after an entity merge rewrote its embedded
  // highlight entity set) without moving the reader — position, paragraph, and page anchor are
  // left untouched, only the section payload is swapped. The merge cleared the section's Dexie
  // row, so this reads through to the server.
  async function reloadCurrentSection(): Promise<void> {
    const summary = manifest.value[currentPosition.value];
    const id = bookId.value;
    if (!summary || id === null) {
      return;
    }
    const { version, canCache } = cacheParams(id);
    currentSection.value = await readerApi.getSection(id, summary.sectionIndex, version, canCache);
  }

  return {
    manifest,
    currentSection,
    currentPosition,
    currentParagraphIndex,
    entryParagraph,
    entryPage,
    navigationRevision,
    isLoadingSection,
    sectionCount,
    currentSectionTitle,
    currentSectionIndex,
    hasNextSection,
    hasPrevSection,
    open,
    goTo,
    goToLocation,
    nextSection,
    prevSection,
    setParagraph,
    reloadCurrentSection,
  };
}

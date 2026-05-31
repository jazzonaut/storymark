import { computed, ref, shallowRef } from "vue";
import type { ComputedRef, Ref, ShallowRef } from "vue";
import type { Section, SectionSummary } from "@storymark/shared-types";
import { readerApi } from "@/api/reader-api";

/** Which edge of a freshly-loaded section the reader should land on: the start
 * when moving forward or opening, the end when paging backward into it. */
export type SectionEntry = "start" | "end";

const bookId = ref<string | null>(null);
const manifest = shallowRef<readonly SectionSummary[]>([]);
const currentPosition = ref(0);
const currentSection = shallowRef<Section | null>(null);
const currentParagraphIndex = ref(0);
const sectionEntry = ref<SectionEntry>("start");
const isLoadingSection = ref(false);

interface UseReader {
  readonly manifest: ShallowRef<readonly SectionSummary[]>;
  readonly currentSection: ShallowRef<Section | null>;
  readonly currentPosition: Ref<number>;
  readonly currentParagraphIndex: Ref<number>;
  readonly sectionEntry: Ref<SectionEntry>;
  readonly isLoadingSection: Ref<boolean>;
  readonly sectionCount: ComputedRef<number>;
  readonly currentSectionTitle: ComputedRef<string | null>;
  readonly hasNextSection: ComputedRef<boolean>;
  readonly hasPrevSection: ComputedRef<boolean>;
  open: (id: string) => Promise<readonly SectionSummary[]>;
  nextSection: () => Promise<boolean>;
  prevSection: () => Promise<boolean>;
  setParagraph: (paragraphIndex: number) => void;
}

const sectionCount = computed<number>(() => manifest.value.length);
const currentSectionTitle = computed<string | null>(
  () => manifest.value[currentPosition.value]?.title ?? null,
);
const hasNextSection = computed<boolean>(() => currentPosition.value < manifest.value.length - 1);
const hasPrevSection = computed<boolean>(() => currentPosition.value > 0);

export function useReader(): UseReader {
  async function loadPosition(position: number, entry: SectionEntry): Promise<void> {
    const summary = manifest.value[position];
    const id = bookId.value;
    if (!summary || id === null) {
      return;
    }
    isLoadingSection.value = true;
    try {
      const section = await readerApi.getSection(id, summary.sectionIndex);
      currentPosition.value = position;
      sectionEntry.value = entry;
      currentParagraphIndex.value =
        entry === "end" ? Math.max(section.paragraphs.length - 1, 0) : 0;
      currentSection.value = section;
    } finally {
      isLoadingSection.value = false;
    }
  }

  async function open(id: string): Promise<readonly SectionSummary[]> {
    bookId.value = id;
    currentSection.value = null;
    currentPosition.value = 0;
    currentParagraphIndex.value = 0;
    manifest.value = await readerApi.getManifest(id);
    await loadPosition(0, "start");
    return manifest.value;
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

  return {
    manifest,
    currentSection,
    currentPosition,
    currentParagraphIndex,
    sectionEntry,
    isLoadingSection,
    sectionCount,
    currentSectionTitle,
    hasNextSection,
    hasPrevSection,
    open,
    nextSection,
    prevSection,
    setParagraph,
  };
}

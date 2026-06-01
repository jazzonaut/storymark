import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";
import type { MemorySnapshot } from "@storymark/shared-types";
import { memoryApi } from "@/api/memory-api";
import type { BookVersion } from "@/db/storymark-db";
import { useReaderSettings } from "@/composables/use-reader-settings";

// Module-level so the card survives reader interactions. `shown` keeps the recap to
// once per section per session. The caller fires this only on a forward advance into
// a new section (not on open/resume/jump), so there's no first-entry to skip.
const snapshot = shallowRef<MemorySnapshot | null>(null);
const visible = ref(false);
const shown = new Set<number>();

interface UseSectionRecap {
  readonly snapshot: ShallowRef<MemorySnapshot | null>;
  readonly visible: Ref<boolean>;
  reset: () => void;
  onSectionEntered: (
    bookId: string,
    enteringSection: number,
    version: BookVersion | null,
    canCache: boolean,
  ) => Promise<void>;
  dismiss: () => void;
  dontShowAgain: () => void;
}

export function useSectionRecap(): UseSectionRecap {
  const { settings, update } = useReaderSettings();

  // Call when a book opens, before reading begins.
  function reset(): void {
    shown.clear();
    snapshot.value = null;
    visible.value = false;
  }

  async function onSectionEntered(
    bookId: string,
    enteringSection: number,
    version: BookVersion | null,
    canCache: boolean,
  ): Promise<void> {
    if (!settings.recapCardOnSectionEntry || shown.has(enteringSection)) {
      return;
    }

    let result: MemorySnapshot | null;
    try {
      result = (await memoryApi.getForSectionEntry(bookId, enteringSection, version, canCache))
        .value;
    } catch {
      // Offline or transient: skip the recap (reading is unaffected) and leave the
      // section unmarked so a later online entry can still surface it.
      return;
    }

    shown.add(enteringSection);
    if (result && result.summarySoFar.trim().length > 0) {
      snapshot.value = result;
      visible.value = true;
    }
  }

  function dismiss(): void {
    visible.value = false;
  }

  function dontShowAgain(): void {
    update("recapCardOnSectionEntry", false);
    visible.value = false;
  }

  return { snapshot, visible, reset, onSectionEntered, dismiss, dontShowAgain };
}

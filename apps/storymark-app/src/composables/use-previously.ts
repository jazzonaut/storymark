import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";
import type { MemorySnapshot } from "@storymark/shared-types";
import { memoryApi } from "@/api/memory-api";
import { useReaderSettings } from "@/composables/use-reader-settings";
import { db } from "@/db/storymark-db";

// A book reopened after this gap gets a "Previously…" recap.
const REOPEN_GAP_HOURS = 12;
const REOPEN_GAP_MS = REOPEN_GAP_HOURS * 60 * 60 * 1000;

const snapshot = shallowRef<MemorySnapshot | null>(null);
const visible = ref(false);

interface UsePreviously {
  readonly snapshot: ShallowRef<MemorySnapshot | null>;
  readonly visible: Ref<boolean>;
  maybeShow: (bookId: string, currentSection: number, currentLocation: number) => Promise<void>;
  dismiss: () => void;
  dontShowAgain: () => void;
}

export function usePreviously(): UsePreviously {
  const { settings, update } = useReaderSettings();

  // Called once when a book opens. Always stamps the open time; shows the recap only
  // when the previous open was more than a day ago (and recaps aren't disabled).
  async function maybeShow(
    bookId: string,
    currentSection: number,
    currentLocation: number,
  ): Promise<void> {
    snapshot.value = null;
    visible.value = false;

    const previous = await db.bookOpens.get(bookId);
    const now = new Date();
    await db.bookOpens.put({ bookId, lastOpenedAt: now.toISOString() });

    if (!previous || !settings.recapCardOnSectionEntry) {
      return;
    }
    if (now.getTime() - new Date(previous.lastOpenedAt).getTime() <= REOPEN_GAP_MS) {
      return;
    }

    let result: MemorySnapshot | null;
    try {
      result = await memoryApi.getAtPosition(bookId, currentSection, currentLocation);
    } catch {
      // Offline or transient: skip the recap, reading is unaffected.
      return;
    }
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

  return { snapshot, visible, maybeShow, dismiss, dontShowAgain };
}

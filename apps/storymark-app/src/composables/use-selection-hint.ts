import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";
import type { SelectionHintResult } from "@storymark/shared-types";
import { selectionApi } from "@/api/selection-api";
import { ApiError } from "@/api/api-error";
import { useLatestOnly } from "@/composables/use-latest-only";

/** A live text selection in the reader: the selected text and the viewport rect
 * of its range, used to anchor the popover. */
export interface ReaderSelection {
  readonly text: string;
  readonly rect: {
    readonly top: number;
    readonly bottom: number;
    readonly left: number;
    readonly width: number;
  };
}

// Module-level: one selection popover is resolving at a time.
const isResolving = ref(false);
const result = shallowRef<SelectionHintResult | null>(null);
const error = ref<string | null>(null);
// Changing the selection while a hint is pending must not let the old request's
// response land in the popover. Each resolve claims a token and commits only if current.
const latest = useLatestOnly();

interface UseSelectionHint {
  readonly isResolving: Ref<boolean>;
  readonly result: ShallowRef<SelectionHintResult | null>;
  readonly error: Ref<string | null>;
  resolve: (
    bookId: string,
    selectedText: string,
    sectionIndex: number,
    currentLocation: number,
  ) => Promise<void>;
  reset: () => void;
}

export function useSelectionHint(): UseSelectionHint {
  async function resolve(
    bookId: string,
    selectedText: string,
    sectionIndex: number,
    currentLocation: number,
  ): Promise<void> {
    const token = latest.begin();
    isResolving.value = true;
    error.value = null;
    result.value = null;
    try {
      // The backend ignores the selection's character range (it queries on the
      // text + reading position); precise offsets are future work for mention
      // recording, so the range fields carry the reading location.
      const hint = await selectionApi.hint(bookId, {
        selectedText,
        sectionIndex,
        locationStart: currentLocation,
        locationEnd: currentLocation,
        currentLocation,
      });
      if (latest.isCurrent(token)) {
        result.value = hint;
      }
    } catch (caught) {
      if (latest.isCurrent(token)) {
        error.value =
          caught instanceof ApiError
            ? "Couldn't look that up just now."
            : "Hints are unavailable offline.";
      }
    } finally {
      if (latest.isCurrent(token)) {
        isResolving.value = false;
      }
    }
  }

  function reset(): void {
    // Claim a token so any in-flight resolve is stale and can't land after reset.
    latest.begin();
    isResolving.value = false;
    result.value = null;
    error.value = null;
  }

  return { isResolving, result, error, resolve, reset };
}

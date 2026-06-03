import type { CurrentScene } from "@storymark/shared-types";
import { fetchJson, LLM_TIMEOUT_MS } from "@/api/http";
import type { BookVersion } from "@/db/storymark-db";

// The card narrates the reader's exact position, so a persisted copy would describe
// a different page after any move — unlike the other memory surfaces it is never
// written to IndexedDB. A single in-memory memo only spares regenerating when the
// panel remounts at an unchanged position (a tab toggle) and dies with the page.
let memo: { readonly key: string; readonly scene: CurrentScene } | null = null;

export const currentSceneApi = {
  /** Generates the Current Scene card at the reader's spoiler boundary. `canMemo`
   * is false while processing is incomplete because the supporting memory/events
   * may still be partial. */
  get: async (
    bookId: string,
    section: number,
    location: number,
    version: BookVersion | null,
    canMemo: boolean,
  ): Promise<CurrentScene> => {
    const key = `${bookId}:${String(section)}:${String(location)}:${version ?? ""}`;
    if (memo !== null && memo.key === key) {
      return memo.scene;
    }
    const scene = await fetchJson<CurrentScene>(
      `/api/books/${encodeURIComponent(bookId)}/scene?section=${String(section)}&location=${String(location)}`,
      "Failed to load current scene",
      {},
      LLM_TIMEOUT_MS,
    );
    if (canMemo) {
      memo = { key, scene };
    }
    return scene;
  },
} as const;

import { ref } from "vue";
import type { Ref } from "vue";
import { db } from "@/db/storymark-db";

export type MemoryTab = "summary" | "timeline" | "graph" | "guide" | "assistant" | "scene";

export interface MemoryTabDef {
  readonly id: MemoryTab;
  readonly label: string;
}

// Tab order is the displayed order and the number-key order (1 = Summary, …).
// Scene is deliberately last and never the default: opening it triggers an LLM
// generation, so it must be an explicit choice rather than the landing tab.
export const MEMORY_TABS: readonly MemoryTabDef[] = [
  { id: "summary", label: "Summary" },
  { id: "timeline", label: "Timeline" },
  { id: "graph", label: "Graph" },
  { id: "guide", label: "Story Guide" },
  { id: "assistant", label: "Assistant" },
  { id: "scene", label: "Scene" },
];

const DEFAULT_TAB: MemoryTab = "summary";
const VALID: ReadonlySet<string> = new Set(MEMORY_TABS.map((tab) => tab.id));

function isMemoryTab(value: string): value is MemoryTab {
  return VALID.has(value);
}

// Module-level: one reader (one panel) is open at a time, like the other reader
// composables.
const activeTab = ref<MemoryTab>(DEFAULT_TAB);
let currentBookId: string | null = null;

interface UseMemoryPanel {
  readonly activeTab: Ref<MemoryTab>;
  readonly tabs: readonly MemoryTabDef[];
  readonly tabCount: number;
  /** Load the persisted last-active tab for a book (called on book open). */
  restore: (bookId: string) => Promise<void>;
  /** Switch tabs and persist the choice for this book. */
  setTab: (tab: MemoryTab) => void;
  /** Switch to the 1-based tab (the number-key shortcut); out-of-range is ignored. */
  selectByIndex: (oneBased: number) => void;
}

export function useMemoryPanel(): UseMemoryPanel {
  async function restore(bookId: string): Promise<void> {
    currentBookId = bookId;
    activeTab.value = DEFAULT_TAB;
    try {
      const row = await db.panelState.get(bookId);
      // Never restore onto Scene: it generates on mount, and a remembered tab must
      // not cost an LLM call before the reader has chosen anything.
      if (row && isMemoryTab(row.lastTab) && row.lastTab !== "scene") {
        activeTab.value = row.lastTab;
      }
    } catch {
      // A read failure just leaves the default tab selected — non-critical.
    }
  }

  function setTab(tab: MemoryTab): void {
    activeTab.value = tab;
    if (currentBookId !== null) {
      void db.panelState.put({ bookId: currentBookId, lastTab: tab });
    }
  }

  function selectByIndex(oneBased: number): void {
    const entry = MEMORY_TABS[oneBased - 1];
    if (entry) {
      setTab(entry.id);
    }
  }

  return {
    activeTab,
    tabs: MEMORY_TABS,
    tabCount: MEMORY_TABS.length,
    restore,
    setTab,
    selectByIndex,
  };
}

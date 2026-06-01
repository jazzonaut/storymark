import Dexie from "dexie";
import type { Table } from "dexie";
import type { MemorySnapshot, Section, SectionSummary } from "@storymark/shared-types";

/** A book's section manifest, cached for offline reading. */
export interface CachedManifest {
  readonly bookId: string;
  readonly sections: readonly SectionSummary[];
}

/** Full section content, cached and keyed by [bookId+sectionIndex]. */
export interface CachedSection extends Section {
  readonly bookId: string;
}

/** A memory snapshot cached for the section-entry recap, keyed by
 * [bookId+enteringSection] — the section whose entry the snapshot recaps (its
 * boundary is the *previous* section). Lets the recap render offline. */
export interface CachedMemory {
  readonly bookId: string;
  readonly enteringSection: number;
  readonly snapshot: MemorySnapshot;
}

/** Reader typography/theme settings. App-global (single-user), so there is
 * exactly one row, keyed by the literal `"global"`. */
export interface StoredReaderSettings {
  readonly id: "global";
  readonly fontFamily: ReaderFontFamily;
  readonly fontSizePx: number;
  readonly lineHeight: number;
  readonly paragraphSpacingEm: number;
  readonly contentWidthPx: number;
  readonly theme: ReaderTheme;
  readonly entityHighlight: EntityHighlightMode;
  readonly recapCardOnSectionEntry: boolean;
}

export type ReaderFontFamily = "serif" | "sans" | "mono";
export type ReaderTheme = "light" | "sepia" | "dark";
export type EntityHighlightMode = "off" | "subtle" | "always";

/** The local copy of a bookmark plus its sync state. `synced=false` means there
 * is a pending operation: a create (`deleted=false`) to POST, or a tombstone
 * (`deleted=true`) to DELETE on the server, flushed on reconnect. */
export interface StoredBookmark {
  readonly id: string;
  readonly bookId: string;
  readonly sectionIndex: number;
  readonly paragraphIndex: number;
  readonly locationStart: number;
  readonly label: string | null;
  readonly createdAt: string;
  readonly synced: boolean;
  readonly deleted: boolean;
}

/** The local mirror of a book's reading position (one row per book). Doubles as
 * the offline-resume cache and the sync queue: `synced=false` means the position
 * has not yet been accepted by the backend and is flushed on reconnect. */
export interface StoredProgress {
  readonly bookId: string;
  readonly sectionIndex: number;
  readonly paragraphIndex: number;
  readonly locationStart: number;
  readonly locationEnd: number;
  readonly clientTimestamp: string;
  readonly synced: boolean;
}

// The `as Dexie & { … }` pattern types the tables without subclassing Dexie,
// which would force definite-assignment (`!`) fields the no-non-null-assertion
// rule forbids. The schema string below is the source of truth for indexes.
type StorymarkDb = Dexie & {
  manifests: Table<CachedManifest, string>;
  sections: Table<CachedSection, [string, number]>;
  readerSettings: Table<StoredReaderSettings, string>;
  progress: Table<StoredProgress, string>;
  bookmarks: Table<StoredBookmark, string>;
  memorySnapshots: Table<CachedMemory, [string, number]>;
};

export const db = new Dexie("storymark") as StorymarkDb;

db.version(1).stores({
  manifests: "bookId",
  sections: "[bookId+sectionIndex]",
  readerSettings: "id",
});

// v2 adds the reading-progress mirror. Keyed by bookId only — `synced` is a
// boolean (not an indexable IndexedDB key type), and with a handful of books the
// reconnect flush filters unsynced rows in memory.
db.version(2).stores({
  progress: "bookId",
});

// v3 adds bookmarks, keyed by the client-owned id and indexed by bookId for
// per-book listing.
db.version(3).stores({
  bookmarks: "id, bookId",
});

// v4 adds the section-entry recap cache, keyed by [bookId+enteringSection] so the
// recap renders offline once seen online (read-through, like sections).
db.version(4).stores({
  memorySnapshots: "[bookId+enteringSection]",
});

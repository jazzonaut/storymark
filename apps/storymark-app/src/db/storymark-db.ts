import Dexie from "dexie";
import type { Table } from "dexie";
import type {
  BookEvent,
  MemorySnapshot,
  Relationship,
  Section,
  SectionSummary,
  StoryGuideEntity,
} from "@storymark/shared-types";

/** A book's section manifest, cached for offline reading. `cachedAt` (epoch ms)
 * drives a short TTL: a fresh row is served directly, a stale one is refreshed
 * online but still rendered offline — so server-side title changes (e.g. a
 * section-title backfill) surface without the reader having to clear storage. */
export interface CachedManifest {
  readonly bookId: string;
  readonly sections: readonly SectionSummary[];
  readonly cachedAt: number;
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

/** The events visible at a section boundary, cached for the timeline tab and keyed
 * by [bookId+upToSection]. `cachedAt` (epoch ms) drives a short TTL: a fresh row is
 * served directly, a stale one is refreshed online but still rendered offline. */
export interface CachedTimeline {
  readonly bookId: string;
  readonly upToSection: number;
  readonly events: readonly BookEvent[];
  readonly cachedAt: number;
}

/** The relationships visible at a section boundary, cached for the graph tab and
 * keyed by [bookId+upToSection]. `cachedAt` (epoch ms) drives a short TTL, exactly
 * like {@link CachedTimeline}: a fresh row is served directly, a stale one is
 * refreshed online but still rendered offline. */
export interface CachedGraph {
  readonly bookId: string;
  readonly upToSection: number;
  readonly relationships: readonly Relationship[];
  readonly cachedAt: number;
}

/** The Story Guide entities visible at a section boundary, cached and keyed by
 * [bookId+upToSection] with a short TTL like {@link CachedTimeline}. The entity
 * list supplies the Story Guide, the timeline's involved-name lookups, and the
 * graph's nodes, so caching it is what lets all three render offline. */
export interface CachedEntities {
  readonly bookId: string;
  readonly upToSection: number;
  readonly entities: readonly StoryGuideEntity[];
  readonly cachedAt: number;
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

/** When a book was last opened (one row per book), in ISO 8601. Drives the
 * "Previously…" recap on reopen after a long gap. */
export interface StoredBookOpen {
  readonly bookId: string;
  readonly lastOpenedAt: string;
}

/** The last-active Memory panel tab for a book (one row per book), so reopening
 * the panel lands on the tab the reader left it on. `lastTab` is stored loosely as
 * a string and validated against the known tabs on read (the tab set can change
 * between releases). */
export interface StoredPanelState {
  readonly bookId: string;
  readonly lastTab: string;
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
  bookOpens: Table<StoredBookOpen, string>;
  timelines: Table<CachedTimeline, [string, number]>;
  graphs: Table<CachedGraph, [string, number]>;
  entities: Table<CachedEntities, [string, number]>;
  panelState: Table<StoredPanelState, string>;
};

export const db = new Dexie("storymark") as StorymarkDb;

// The schema string is the source of truth for indexes. `synced`/`deleted` flags
// are not indexed — booleans aren't valid IndexedDB key types, and with a handful
// of books the reconnect flush filters unsynced rows in memory.
db.version(1).stores({
  manifests: "bookId",
  sections: "[bookId+sectionIndex]",
  readerSettings: "id",
  progress: "bookId",
  bookmarks: "id, bookId",
  memorySnapshots: "[bookId+enteringSection]",
  bookOpens: "bookId",
  timelines: "[bookId+upToSection]",
  graphs: "[bookId+upToSection]",
  entities: "[bookId+upToSection]",
  panelState: "bookId",
});

// A release that adds a db.version() above upgrades the on-device database on
// first open, and IndexedDB runs that upgrade only once every *other* connection
// to it has closed. If a second tab — or the still-resident installed PWA — holds
// the previous version open, the upgrade blocks indefinitely and every query
// against `db` hangs with no error (a silent "stuck on first load" after a release
// that adds a store). Closing on `versionchange` yields to the newer instance
// instead of deadlocking against it.
db.on("versionchange", () => {
  db.close();
});

/** Removes every locally-cached row for a book once it has been deleted on the
 * server, so nothing orphans in IndexedDB. Compound-key stores are range-deleted
 * across the book's whole key span; single-key stores delete by their key. */
export async function purgeBookData(bookId: string): Promise<void> {
  await Promise.all([
    db.manifests.delete(bookId),
    db.progress.delete(bookId),
    db.bookOpens.delete(bookId),
    db.panelState.delete(bookId),
    db.bookmarks.where("bookId").equals(bookId).delete(),
    db.sections
      .where("[bookId+sectionIndex]")
      .between([bookId, Dexie.minKey], [bookId, Dexie.maxKey])
      .delete(),
    db.memorySnapshots
      .where("[bookId+enteringSection]")
      .between([bookId, Dexie.minKey], [bookId, Dexie.maxKey])
      .delete(),
    db.timelines
      .where("[bookId+upToSection]")
      .between([bookId, Dexie.minKey], [bookId, Dexie.maxKey])
      .delete(),
    db.graphs
      .where("[bookId+upToSection]")
      .between([bookId, Dexie.minKey], [bookId, Dexie.maxKey])
      .delete(),
    db.entities
      .where("[bookId+upToSection]")
      .between([bookId, Dexie.minKey], [bookId, Dexie.maxKey])
      .delete(),
  ]);
}

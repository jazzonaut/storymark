import Dexie from "dexie";
import type { Table } from "dexie";
import type {
  BookEvent,
  BookSummary,
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

/** Full section content, cached and keyed by [bookId+sectionIndex]. Tagged with the
 * book's extraction `version` (like the memory surfaces below) so a reprocess — which
 * rewrites section HTML, paragraph offsets, or the visible-entities array — invalidates
 * the row instead of leaving stale content in IndexedDB forever. Absent on rows cached
 * before versioning, which are therefore never treated as current. */
export interface CachedSection extends Section {
  readonly bookId: string;
  readonly version?: BookVersion;
}

/** A memory snapshot cached for the section-entry recap, keyed by
 * [bookId+enteringSection] — the section whose entry the snapshot recaps (its
 * boundary is the *previous* section). Lets the recap render offline. */
export interface CachedMemory {
  readonly bookId: string;
  readonly enteringSection: number;
  readonly snapshot: MemorySnapshot;
  readonly version?: BookVersion;
}

/** The book's extraction version — its `updatedAt`, bumped whenever a processing job
 * rewrites derived data. Stored on each cached memory surface so a reprocess
 * invalidates it (the row is refetched rather than reused). Absent on rows cached
 * before versioning, which are therefore never treated as current. */
export type BookVersion = string;

/** The events visible at a reading position, cached for the timeline tab and keyed
 * by [bookId+upToSection]. `upToLocation` records the in-section position the row was
 * built at: the backend filters events to `(upToSection, upToLocation)`, so the
 * read-through cache serves a row only at or behind that boundary, never ahead of it
 * (which would leak later-in-section events). `cachedAt` (epoch ms) drives a short
 * TTL: a fresh exact-boundary row is served directly, an older one is refreshed online
 * but still rendered offline as a safe subset. */
export interface CachedTimeline {
  readonly bookId: string;
  readonly upToSection: number;
  readonly upToLocation: number;
  readonly events: readonly BookEvent[];
  readonly cachedAt: number;
  readonly version?: BookVersion;
}

/** The relationships visible at a reading position, cached for the graph tab and
 * keyed by [bookId+upToSection]. `upToLocation` and the TTL behave exactly like
 * {@link CachedTimeline}: the row is gated to the reader's boundary so a relationship
 * first seen later in the section is never served ahead of it. */
export interface CachedGraph {
  readonly bookId: string;
  readonly upToSection: number;
  readonly upToLocation: number;
  readonly relationships: readonly Relationship[];
  readonly cachedAt: number;
  readonly version?: BookVersion;
}

/** The Story Guide entities visible at a reading position, cached and keyed by
 * [bookId+upToSection] with the same boundary gating and TTL as {@link CachedTimeline}.
 * The entity list supplies the Story Guide, the timeline's involved-name lookups, and
 * the graph's nodes, so caching it is what lets all three render offline. */
export interface CachedEntities {
  readonly bookId: string;
  readonly upToSection: number;
  readonly upToLocation: number;
  readonly entities: readonly StoryGuideEntity[];
  readonly cachedAt: number;
  readonly version?: BookVersion;
}

/** The library list, cached as a single row keyed by the literal `"all"`. The list
 * is the one reader read path with no Dexie cache of its own — it leant entirely on
 * the service worker's NetworkFirst cache, so an unreachable API (e.g. off-VPN) with
 * a cold SW cache showed an empty library. `cachedAt` (epoch ms) is informational;
 * the list is fetched network-first and this row is the fallback. */
export interface CachedLibrary {
  readonly id: "all";
  readonly books: readonly BookSummary[];
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
  readonly pageMarginPx: number;
  readonly theme: ReaderTheme;
  readonly entityHighlight: EntityHighlightMode;
  readonly recapCardOnSectionEntry: boolean;
  readonly fullscreen: boolean;
}

export type ReaderFontFamily = "serif" | "sans" | "mono" | "dyslexic" | "lexend";
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
  /** Exact paginated column within the section on this device/layout. This is
   * local-only: server sync remains paragraph/location based because page numbers
   * are not portable across devices, viewport sizes, or reader settings. */
  readonly pageIndex?: number;
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
  library: Table<CachedLibrary, string>;
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

// v2 adds the library list cache. Dexie carries the v1 stores forward, so only the
// new store is declared here. The single row is keyed by the literal "all".
db.version(2).stores({
  library: "id",
});

// v3 adds `upToLocation` to the timeline/graph/entity rows so the read-through cache
// can gate them by the reader's in-section position (see CachedTimeline). The key
// (still [bookId+upToSection]) is unchanged, so no schema delta — but legacy rows
// lack the new field, which would defeat the boundary check, so they are cleared and
// refetched on next open.
db.version(3).upgrade(async (tx) => {
  await Promise.all([
    tx.table("timelines").clear(),
    tx.table("graphs").clear(),
    tx.table("entities").clear(),
  ]);
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

/** Drops the cached entity-dependent surfaces for a book — the Story Guide list, the
 * timeline, the relationship graph, and section content — across their whole key span. An
 * entity merge rewrites entity identities, event involvements, and graph edges, so every
 * snapshot of these becomes stale; section rows also embed the per-section highlight entity
 * set, so a stale row would keep a folded-away entity's id (a 404 on tap). Clearing them
 * forces a refetch from the server (the source of truth) on the next load. Memory snapshots
 * are untouched: a merge doesn't change them. */
export async function purgeEntityDerivedCache(bookId: string): Promise<void> {
  await Promise.all([
    db.entities
      .where("[bookId+upToSection]")
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
    db.sections
      .where("[bookId+sectionIndex]")
      .between([bookId, Dexie.minKey], [bookId, Dexie.maxKey])
      .delete(),
  ]);
}

/** The most recently opened book on this device, or null if none has been opened.
 * Drives the cold-load redirect that resumes the reader's last book instead of the
 * library. `lastOpenedAt` is an ISO 8601 string, so a lexicographic max is also the
 * chronological one; with a handful of books an in-memory scan beats indexing it. */
export async function getLastOpenedBookId(): Promise<string | null> {
  const opens = await db.bookOpens.toArray();
  if (opens.length === 0) {
    return null;
  }
  return opens.reduce((latest, row) => (row.lastOpenedAt > latest.lastOpenedAt ? row : latest))
    .bookId;
}

/** True if any locally-queued reading-position or bookmark write hasn't reached the
 * server yet. {@link clearCachedData} drops the sync queue, so the caller flushes and
 * re-checks this first to avoid discarding unsynced changes. */
export async function hasUnsyncedChanges(): Promise<boolean> {
  const [progress, bookmarks] = await Promise.all([
    db.progress.filter((row) => !row.synced).count(),
    db.bookmarks.filter((row) => !row.synced).count(),
  ]);
  return progress > 0 || bookmarks > 0;
}

/** Clears every locally-cached and sync-queue table, keeping only the reader's
 * settings. Everything cleared re-hydrates from the server (the source of truth) on
 * next load, so flush pending writes and confirm none remain (via
 * {@link hasUnsyncedChanges}) before calling this. */
export async function clearCachedData(): Promise<void> {
  await Promise.all(
    db.tables.filter((table) => table.name !== "readerSettings").map((table) => table.clear()),
  );
}

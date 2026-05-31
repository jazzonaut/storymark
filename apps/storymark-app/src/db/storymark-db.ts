import Dexie from "dexie";
import type { Table } from "dexie";
import type { Section, SectionSummary } from "@storymark/shared-types";

/** A book's section manifest, cached for offline reading. */
export interface CachedManifest {
  readonly bookId: string;
  readonly sections: readonly SectionSummary[];
}

/** Full section content, cached and keyed by [bookId+sectionIndex]. */
export interface CachedSection extends Section {
  readonly bookId: string;
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
}

export type ReaderFontFamily = "serif" | "sans" | "mono";
export type ReaderTheme = "light" | "sepia" | "dark";
export type EntityHighlightMode = "off" | "subtle" | "always";

// The `as Dexie & { … }` pattern types the tables without subclassing Dexie,
// which would force definite-assignment (`!`) fields the no-non-null-assertion
// rule forbids. The schema string below is the source of truth for indexes.
type StorymarkDb = Dexie & {
  manifests: Table<CachedManifest, string>;
  sections: Table<CachedSection, [string, number]>;
  readerSettings: Table<StoredReaderSettings, string>;
};

export const db = new Dexie("storymark") as StorymarkDb;

db.version(1).stores({
  manifests: "bookId",
  sections: "[bookId+sectionIndex]",
  readerSettings: "id",
});

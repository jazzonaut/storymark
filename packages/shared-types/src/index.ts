// Public API for @storymark/shared-types. Per docs/conventions/01-typescript.md
// this is the one place a barrel file is allowed: a package's public surface.

export type ProcessingStatus =
  | "Uploaded"
  | "Parsing"
  | "Parsed"
  | "Chunking"
  | "DetectingEntities"
  | "ExtractingPerSection"
  | "Embedding"
  | "BuildingMemorySnapshots"
  | "Indexed"
  | "Failed";

/** Embedding progress while a book is on `Embedding`: chunks vectorised under the
 * current model over the book's total chunks. Mirrors EmbeddingProgressResponse. */
export interface EmbeddingProgress {
  readonly embedded: number;
  readonly total: number;
}

export interface BookSummary {
  readonly bookId: string;
  readonly title: string;
  readonly author: string | null;
  readonly format: string;
  readonly coverUrl: string;
  readonly processingStatus: ProcessingStatus;
  /** Why the last processing attempt failed, or null. Present even mid-retry. */
  readonly processingError: string | null;
  /** Live embedding progress while `processingStatus` is `Embedding`, else null. */
  readonly embeddingProgress: EmbeddingProgress | null;
  readonly lastReadSection: number | null;
  readonly lastOpenedAt: string | null;
}

/** One entry in the section manifest (`GET /api/books/{id}/sections`).
 * Mirrors SectionSummaryView in apps/storymark-api/Services/Books/SectionViews.cs. */
export interface SectionSummary {
  readonly sectionIndex: number;
  readonly title: string | null;
  readonly wordCount: number;
  readonly paragraphCount: number;
}

/** A paragraph within a section. `locationStart`/`locationEnd` are half-open
 * character offsets into the section's plain-text rendering — the shared
 * coordinate space for position tracking, chunking, and retrieval.
 * Mirrors ParagraphView. */
export interface Paragraph {
  readonly paragraphIndex: number;
  readonly locationStart: number;
  readonly locationEnd: number;
  readonly html: string | null;
  readonly text: string;
}

/** Full section content (`GET /api/books/{id}/sections/{idx}`): sanitised HTML
 * plus paragraph offsets. Mirrors SectionContentView. */
export interface Section {
  readonly sectionIndex: number;
  readonly title: string | null;
  readonly html: string;
  readonly paragraphs: readonly Paragraph[];
}

/** A saved bookmark (`GET /api/books/{id}/bookmarks`). Mirrors BookmarkView.
 * `id` is a client-supplied UUID so create/delete are idempotent offline. */
export interface Bookmark {
  readonly id: string;
  readonly sectionIndex: number;
  readonly paragraphIndex: number | null;
  readonly locationStart: number;
  readonly label: string | null;
  readonly createdAt: string;
}

/** The canonical reading position for a book — the latest across all devices
 * (`GET /api/books/{id}/progress`). Mirrors ProgressView. */
export interface ReadingProgress {
  readonly sectionIndex: number;
  readonly paragraphIndex: number | null;
  readonly locationStart: number;
  readonly locationEnd: number;
  readonly clientTimestamp: string;
}

/** Coarse confidence the assistant attaches to an answer, derived from the
 * strength of the retrieved evidence. */
export type AnswerConfidence = "low" | "medium" | "high";

/** One evidence chunk a Q&A answer cited. `index` is the 1-based citation
 * number the answer text refers to inline (e.g. `[2]`), letting the UI link a
 * marker to its passage. `locationStart`/`locationEnd` are offsets into the
 * section's plain text (the shared coordinate space), so a source can be
 * navigated to in the reader. Mirrors QuestionSource. */
export interface AssistantSource {
  readonly index: number;
  readonly sectionIndex: number;
  readonly locationStart: number;
  readonly locationEnd: number;
  readonly snippet: string;
}

/** A spoiler-safe answer (`POST /api/books/{id}/ask`). `usedFutureContent` is
 * derived from the evidence and is always `false` unless the retrieval filter
 * regresses. Mirrors AskResponse. */
export interface AssistantAnswer {
  readonly answer: string;
  readonly confidence: AnswerConfidence;
  readonly sources: readonly AssistantSource[];
  readonly usedFutureContent: boolean;
}

/** The spoiler-safe entity card returned by the selection-hint entity
 * short-circuit. Mirrors EntityCard. */
export interface SelectionEntityCard {
  readonly canonicalName: string;
  readonly entityType: string;
  readonly aliases: readonly string[];
  readonly firstSeenSection: number | null;
}

/** One evidence chunk behind a selection hint. Mirrors HintSource. */
export interface SelectionHintSource {
  readonly sectionIndex: number;
  readonly locationStart: number;
  readonly locationEnd: number;
}

/** The tiered result of a selection (`POST /api/books/{id}/selection/hint`):
 * either a cached entity card (instant, zero LLM cost) or a generated hint.
 * Discriminated on `kind`. Mirrors SelectionHintResponse. */
export type SelectionHintResult =
  | { readonly kind: "entity"; readonly entityId: string; readonly card: SelectionEntityCard }
  | {
      readonly kind: "hint";
      readonly hint: string;
      readonly sources: readonly SelectionHintSource[];
    };

/** The rolling "story so far" at-or-before a reading position
 * (`GET /api/books/{id}/memory`) — the data source for the section-entry recap
 * card. `upToSection`/`upToLocation` mark the snapshot's boundary (end of that
 * section). Mirrors MemoryView. */
export interface MemorySnapshot {
  readonly upToSection: number;
  readonly upToLocation: number;
  readonly summarySoFar: string;
  readonly timeline: readonly string[];
  readonly openThreads: readonly string[];
}

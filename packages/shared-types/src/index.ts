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

/** Extraction progress while a book is on `ExtractingPerSection`: section summaries
 * written over the book's total sections. Mirrors ExtractionProgressResponse. */
export interface ExtractionProgress {
  readonly extracted: number;
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
  /** Live extraction progress while `processingStatus` is `ExtractingPerSection`, else null. */
  readonly extractionProgress: ExtractionProgress | null;
  readonly lastReadSection: number | null;
  readonly lastOpenedAt: string | null;
  /** Bumped (ISO 8601) whenever a processing job rewrites the book's derived data
   * — extraction, embedding, or memory snapshots. The client stores it with each
   * cached memory surface (timeline, graph, Story Guide, summary) as a version
   * token, so a reprocess invalidates those caches. */
  readonly updatedAt: string;
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

/** A Story Guide entity visible at a reading position, carried on a section
 * payload (for in-text highlighting) and the entities list. Mirrors
 * SectionEntityView. */
export interface SectionEntity {
  readonly entityId: string;
  readonly canonicalName: string;
  readonly entityType: string;
  readonly aliases: readonly string[];
  readonly firstSeenSection: number | null;
}

/** A Story Guide list entry (`GET /api/books/{id}/entities`): a visible entity
 * plus its latest cached short summary at-or-before the position, or null if none
 * has been generated yet. Mirrors StoryGuideEntityView. */
export interface StoryGuideEntity extends SectionEntity {
  readonly shortSummary: string | null;
}

/** A spoiler-safe entity summary
 * (`GET /api/books/{id}/entities/{entityId}/summary`), generated lazily on first
 * tap and cached server-side. Mirrors EntitySummaryView. */
export interface EntitySummary {
  readonly canonicalName: string;
  readonly entityType: string;
  readonly shortSummary: string;
  readonly knownFacts: readonly string[];
  readonly relationships: readonly string[];
  readonly openQuestions: readonly string[];
}

/** Full section content (`GET /api/books/{id}/sections/{idx}`): sanitised HTML,
 * paragraph offsets, and the entities visible by this section (for highlighting).
 * Mirrors SectionContentView. */
export interface Section {
  readonly sectionIndex: number;
  readonly title: string | null;
  readonly html: string;
  readonly paragraphs: readonly Paragraph[];
  readonly entities: readonly SectionEntity[];
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

/** One earlier Q&A turn, carried by the client with a follow-up question so the
 * model can resolve references like "she" — the server keeps no conversation
 * state. Mirrors AskHistoryTurn. */
export interface AssistantHistoryTurn {
  readonly question: string;
  readonly answer: string;
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

/** An extracted story event visible at a reading position
 * (`GET /api/books/{id}/events`). `locationStart` is where the timeline navigates
 * to; `eventType` is a free-text label from the model (sometimes a `a | b` option
 * list) and may be null. Mirrors EventView. */
export interface BookEvent {
  readonly sectionIndex: number;
  readonly locationStart: number;
  readonly locationEnd: number;
  readonly eventType: string | null;
  readonly description: string;
  readonly involvedEntityIds: readonly string[];
}

/** An extracted relationship between two entities, visible at a reading position
 * (`GET /api/books/{id}/relationships`). `fromEntityId`/`toEntityId` reference
 * reconciled `book_entities` ids; `polarity` is a free-text sentiment label
 * (e.g. "positive") and may be null. Mirrors RelationshipView. */
export interface Relationship {
  readonly fromEntityId: string;
  readonly toEntityId: string;
  readonly relationshipType: string;
  readonly polarity: string | null;
  readonly evidence: string | null;
  readonly firstSeenSection: number;
  readonly firstSeenLocation: number;
}

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

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

export interface BookSummary {
  readonly bookId: string;
  readonly title: string;
  readonly author: string | null;
  readonly format: string;
  readonly coverUrl: string;
  readonly processingStatus: ProcessingStatus;
  readonly lastReadSection: number | null;
  readonly lastOpenedAt: string | null;
}

namespace Storymark.Api.Domain.Books;

/// <summary>Lifecycle of an imported book, persisted as text and exposed to the
/// frontend (docs/plan.md §15.4). S1.1 only ever sets <see cref="Uploaded"/>;
/// the later states are driven by the processing pipeline from S2.1 onward.</summary>
internal enum ProcessingStatus
{
    Uploaded,
    Parsing,
    Parsed,
    Chunking,
    DetectingEntities,
    ExtractingPerSection,
    Embedding,
    BuildingMemorySnapshots,
    Indexed,
    Failed,
}

using Storymark.Api.Domain.Books;

namespace Storymark.Api.Services.Books;

/// <summary>A catalogue row plus its mid-pipeline progress (chunks vectorised under
/// the current model / total chunks for embedding; section summaries written / total
/// sections for extraction), so the library list can show how far a book has got
/// without the endpoint touching those tables.</summary>
internal sealed record BookCatalogEntry
{
    public required Book Book { get; init; }

    public required int EmbeddedChunks { get; init; }

    public required int TotalChunks { get; init; }

    public required int ExtractedSections { get; init; }

    public required int TotalSections { get; init; }
}

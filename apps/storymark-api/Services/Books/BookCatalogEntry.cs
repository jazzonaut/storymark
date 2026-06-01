using Storymark.Api.Domain.Books;

namespace Storymark.Api.Services.Books;

/// <summary>A catalogue row plus its embedding progress (chunks vectorised under
/// the current model / total chunks), so the library list can show how far a
/// mid-pipeline book has got without the endpoint touching the chunk table.</summary>
internal sealed record BookCatalogEntry
{
    public required Book Book { get; init; }

    public required int EmbeddedChunks { get; init; }

    public required int TotalChunks { get; init; }
}

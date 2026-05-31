namespace Storymark.Api.Services.Books;

/// <summary>A reading position to persist (<c>POST /api/books/{id}/progress</c>).</summary>
internal sealed record SaveProgressInput
{
    public required string DeviceId { get; init; }

    public required int SectionIndex { get; init; }

    public int? ParagraphIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required DateTimeOffset ClientTimestamp { get; init; }
}

/// <summary>The canonical reading position for a book: the latest across all
/// devices (<c>GET /api/books/{id}/progress</c>).</summary>
internal sealed record ProgressView
{
    public required int SectionIndex { get; init; }

    public int? ParagraphIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required DateTimeOffset ClientTimestamp { get; init; }
}

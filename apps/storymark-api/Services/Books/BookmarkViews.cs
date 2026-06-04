namespace Storymark.Api.Services.Books;

/// <summary>A bookmark to create or replace (<c>POST /api/books/{id}/bookmarks</c>).
/// The client owns <see cref="Id"/> so the write is an idempotent upsert.</summary>
internal sealed record SaveBookmarkInput
{
    public required Guid Id { get; init; }

    public required int SectionIndex { get; init; }

    public int? ParagraphIndex { get; init; }

    public required int LocationStart { get; init; }

    public string? Label { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>A saved bookmark (<c>GET /api/books/{id}/bookmarks</c>).</summary>
internal sealed record BookmarkView
{
    public required Guid Id { get; init; }

    public required int SectionIndex { get; init; }

    public int? ParagraphIndex { get; init; }

    public required int LocationStart { get; init; }

    public string? Label { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

internal enum BookmarkUpsertOutcome
{
    Saved,
    BookNotFound,

    /// <summary>The bookmark id already belongs to a different book or a different
    /// reader. The route book and the session user scope the mutation, so the write
    /// is rejected rather than moving the bookmark.</summary>
    IdBelongsToAnotherOwner,
}

/// <summary>The result of <see cref="BookmarkService.UpsertAsync"/>: an outcome plus the
/// saved <see cref="BookmarkView"/> when (and only when) the outcome is
/// <see cref="BookmarkUpsertOutcome.Saved"/>.</summary>
internal sealed record BookmarkUpsertResult
{
    public required BookmarkUpsertOutcome Outcome { get; init; }

    public BookmarkView? View { get; init; }
}

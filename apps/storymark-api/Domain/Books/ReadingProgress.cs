namespace Storymark.Api.Domain.Books;

/// <summary>A device's last reading position in a book (docs/plan.md §12.5).
/// One row per <c>(BookId, DeviceId)</c>; the canonical position for a book is
/// the row with the greatest <see cref="ClientTimestamp"/> across all devices
/// (last-write-wins, resolved on read).</summary>
internal sealed class ReadingProgress
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public required string DeviceId { get; set; }

    public int SectionIndex { get; set; }

    public int? ParagraphIndex { get; set; }

    public int LocationStart { get; set; }

    public int LocationEnd { get; set; }

    /// <summary>Client-supplied wall-clock of the read; the tiebreaker for
    /// last-write-wins so a late offline flush can't overwrite a newer position.</summary>
    public DateTimeOffset ClientTimestamp { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

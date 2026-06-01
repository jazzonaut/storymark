namespace Storymark.Api.Domain.Books;

/// <summary>A device's last reading position in a book (docs/plan.md §12.5).
/// One row per <c>(BookId, DeviceId)</c>; the canonical position for a book is the
/// most recently synced row across all devices — ordered by <see cref="UpdatedAt"/>
/// (server receive time), resolved on read. Server time rather than the client
/// wall-clock so a skewed device clock can't pin the book to an old position.</summary>
internal sealed class ReadingProgress
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public required string DeviceId { get; set; }

    public int SectionIndex { get; set; }

    public int? ParagraphIndex { get; set; }

    public int LocationStart { get; set; }

    public int LocationEnd { get; set; }

    /// <summary>Client-supplied wall-clock of the read. Used only for same-device
    /// stale-flush detection (a late offline flush can't overwrite this device's newer
    /// row), where one device's clock is internally consistent. Cross-device canonical
    /// ordering uses <see cref="UpdatedAt"/>, not this.</summary>
    public DateTimeOffset ClientTimestamp { get; set; }

    /// <summary>Server receive time of the last accepted write; the cross-device
    /// canonical-ordering key.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

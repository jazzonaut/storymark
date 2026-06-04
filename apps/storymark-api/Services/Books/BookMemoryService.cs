using Microsoft.EntityFrameworkCore;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Books;

/// <summary>Read access to the rolling memory snapshots (docs/plan.md §5.2). Returns
/// the latest snapshot at-or-before a reading position so the recap card shows the
/// "story so far" without leaking anything past the reader's position.</summary>
internal sealed class BookMemoryService(StorymarkDbContext db)
{
    /// <summary>The most recent snapshot whose boundary is at-or-before
    /// <c>(upToSection, upToLocation)</c>, or <c>null</c> if none exists yet.
    /// Callers gate visibility first (the memory endpoint's IsVisibleAsync,
    /// CurrentSceneService's own gate) — exactly once, so this method does not
    /// re-run the check.</summary>
    public async Task<MemoryView?> GetLatestAsync(
        Guid bookId, int upToSection, int upToLocation, CancellationToken ct) =>
        await db.BookMemorySnapshots.AsNoTracking()
            .Where(s => s.BookId == bookId
                && (s.UpToSection < upToSection
                    || (s.UpToSection == upToSection && s.UpToLocation <= upToLocation)))
            .OrderByDescending(s => s.UpToSection)
            .ThenByDescending(s => s.UpToLocation)
            .Select(s => new MemoryView
            {
                UpToSection = s.UpToSection,
                UpToLocation = s.UpToLocation,
                SummarySoFar = s.SummarySoFar,
                Timeline = s.Timeline,
                OpenThreads = s.OpenThreads,
            })
            .FirstOrDefaultAsync(ct);
}

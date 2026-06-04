using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Books;

/// <summary>Backend-authoritative reading progress (docs/plan.md §12.5). One row
/// per <c>(book, user, device)</c>; a reader's canonical position is the latest
/// across their devices.</summary>
internal sealed class ReadingProgressService(StorymarkDbContext db)
{
    /// <summary>Upserts the given device's position for the reader. Returns the
    /// canonical latest position after the write, or <c>null</c> if the book does
    /// not exist.</summary>
    public async Task<ProgressView?> SaveAsync(
        Guid bookId,
        Guid userId,
        SaveProgressInput input,
        CancellationToken ct)
    {
        if (!await db.Books.IsVisibleAsync(bookId, userId, ct))
        {
            return null;
        }

        var existing = await db.ReadingProgress.FirstOrDefaultAsync(
            p => p.BookId == bookId && p.UserId == userId && p.DeviceId == input.DeviceId, ct);

        if (existing is null)
        {
            db.ReadingProgress.Add(new ReadingProgress
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                UserId = userId,
                DeviceId = input.DeviceId,
                SectionIndex = input.SectionIndex,
                ParagraphIndex = input.ParagraphIndex,
                LocationStart = input.LocationStart,
                LocationEnd = input.LocationEnd,
                ClientTimestamp = input.ClientTimestamp,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        else if (input.ClientTimestamp >= existing.ClientTimestamp)
        {
            // Ignore an out-of-order (stale) flush so it can't overwrite a newer
            // position already recorded for this device.
            existing.SectionIndex = input.SectionIndex;
            existing.ParagraphIndex = input.ParagraphIndex;
            existing.LocationStart = input.LocationStart;
            existing.LocationEnd = input.LocationEnd;
            existing.ClientTimestamp = input.ClientTimestamp;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return await GetLatestAsync(bookId, userId, ct);
    }

    /// <summary>The reader's latest position across all their devices, or <c>null</c>
    /// if there is none (or the book does not exist — callers needing that distinction
    /// gate on IsVisibleAsync first, as the progress endpoint does).
    ///
    /// <para>Cross-device ordering is by server receive time (<see cref="ReadingProgress.UpdatedAt"/>),
    /// not the client wall-clock: a device with a skewed clock can otherwise pin the book
    /// to an old position with a future <see cref="ReadingProgress.ClientTimestamp"/>. The
    /// client timestamp stays the same-device stale-flush guard in <see cref="SaveAsync"/>,
    /// where it's internally consistent.</para></summary>
    public Task<ProgressView?> GetLatestAsync(Guid bookId, Guid userId, CancellationToken ct)
    {
        return db.ReadingProgress.AsNoTracking()
            .Where(p => p.BookId == bookId && p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new ProgressView
            {
                SectionIndex = p.SectionIndex,
                ParagraphIndex = p.ParagraphIndex,
                LocationStart = p.LocationStart,
                LocationEnd = p.LocationEnd,
                ClientTimestamp = p.ClientTimestamp,
            })
            .FirstOrDefaultAsync(ct);
    }
}

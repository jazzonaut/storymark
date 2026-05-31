using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Books;

/// <summary>Backend-authoritative reading progress (docs/plan.md §12.5). One row
/// per <c>(book, device)</c>; the canonical position is the latest across devices.</summary>
internal sealed class ReadingProgressService(StorymarkDbContext db)
{
    /// <summary>Upserts the given device's position. Returns the canonical latest
    /// position after the write, or <c>null</c> if the book does not exist.</summary>
    public async Task<ProgressView?> SaveAsync(Guid bookId, SaveProgressInput input, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        var existing = await db.ReadingProgress
            .FirstOrDefaultAsync(p => p.BookId == bookId && p.DeviceId == input.DeviceId, ct);

        if (existing is null)
        {
            db.ReadingProgress.Add(new ReadingProgress
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
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

        return await GetLatestAsync(bookId, ct);
    }

    /// <summary>The latest position across all devices, or <c>null</c> if there is
    /// none (or the book does not exist — callers needing that distinction use
    /// <see cref="BookExistsAsync"/>).</summary>
    public Task<ProgressView?> GetLatestAsync(Guid bookId, CancellationToken ct)
    {
        return db.ReadingProgress.AsNoTracking()
            .Where(p => p.BookId == bookId)
            .OrderByDescending(p => p.ClientTimestamp)
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

    public Task<bool> BookExistsAsync(Guid bookId, CancellationToken ct)
    {
        return db.Books.AnyAsync(b => b.Id == bookId, ct);
    }
}

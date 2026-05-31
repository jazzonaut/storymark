using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.LlmCalls;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Llm;

/// <summary>Read access to the LLM call log for the admin call-log view.</summary>
internal sealed class LlmCallLogService(StorymarkDbContext db)
{
    public async Task<IReadOnlyList<LlmCall>> GetRecentAsync(string? task, int limit, CancellationToken ct)
    {
        var query = db.LlmCalls.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(task))
        {
            query = query.Where(c => c.Task == task);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}

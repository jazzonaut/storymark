using FastEndpoints;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Jobs;

namespace Storymark.Api.Endpoints.Admin.Books;

internal sealed record BackfillSectionTitlesResponse
{
    public required int SectionsToBackfill { get; init; }
}

/// <summary>Enqueues <see cref="BackfillSectionTitlesJob"/>, which gives untitled
/// sections a title from their first stored heading (a cheap, display-only fix — no
/// re-parse, LLM, or re-embed). Returns the count of currently untitled sections so the
/// caller sees the scope; the job logs the count actually filled. Admin-only.</summary>
internal sealed class BackfillSectionTitlesEndpoint(
    StorymarkDbContext db,
    IBackgroundJobClient jobs)
    : EndpointWithoutRequest<BackfillSectionTitlesResponse>
{
    public override void Configure()
    {
        Post("books/backfill-section-titles");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pending = await db.BookSections.CountAsync(s => s.Title == null && s.HtmlPath != null, ct);
        jobs.Enqueue<BackfillSectionTitlesJob>(j => j.RunAsync(CancellationToken.None));

        await Send.OkAsync(new BackfillSectionTitlesResponse { SectionsToBackfill = pending }, ct);
    }
}

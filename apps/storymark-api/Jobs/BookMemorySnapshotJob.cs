using System.Globalization;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Llm;
using Storymark.Api.Services.Extraction;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Memory;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Jobs;

/// <summary>Builds the rolling "story so far" snapshots (docs/plan.md §15.2, §12.11).
/// For each section that has a summary, in order, it folds the previous snapshot and
/// the new section summary into an updated memory via one <c>task:"memory"</c> call
/// (<c>prompts.bookMemory</c>), and persists a <see cref="BookMemorySnapshot"/> keyed
/// on the section boundary. This is the <c>BuildingMemorySnapshots</c> step:
/// <see cref="SectionExtractionJob"/> enqueues it, and on completion it advances the
/// book to <c>Embedding</c> and enqueues <see cref="EmbeddingJob"/> — keeping the
/// embedding step terminal/reindex-safe (ADR-0018). Memory needs only section
/// summaries, not vectors, so it runs before embedding.
///
/// <para>Tolerant: an unparseable composition retries once stricter, then degrades to
/// a deterministic merge (previous summary + this section's summary) so the snapshot
/// chain and the row always survive. A missing memory model
/// (<see cref="ModelNotFoundException"/>) fails the book fast. Idempotent: a re-run
/// clears the book's snapshots first.</para></summary>
internal sealed class BookMemorySnapshotJob(
    StorymarkDbContext db,
    BookProcessingRun run,
    ChatModelRouter router,
    ISettingsService settings,
    IBackgroundJobClient jobs,
    ILogger<BookMemorySnapshotJob> logger)
{
    public Task RunAsync(Guid bookId, CancellationToken ct) => RunAsync(bookId, Guid.Empty, ct);

    public Task RunAsync(Guid bookId, Guid runId, CancellationToken ct) =>
        run.ExecuteAsync(bookId, runId, ProcessAsync, ct);

    private async Task ProcessAsync(Book book, CancellationToken ct)
    {
        var bookId = book.Id;
        if (book.ProcessingStatus != ProcessingStatus.BuildingMemorySnapshots)
        {
            return;
        }

        try
        {
            await db.BookMemorySnapshots.Where(s => s.BookId == bookId).ExecuteDeleteAsync(ct);
            await BuildAllAsync(book, ct);

            await run.AdvanceAsync(book, ProcessingStatus.Embedding,
                () => jobs.Enqueue<EmbeddingJob>(j => j.RunAsync(book.Id, book.ProcessingRunId, CancellationToken.None)), ct);
        }
        catch (ModelNotFoundException ex)
        {
            logger.LogError(ex, "BookMemorySnapshotJob failed for book {BookId}: model unavailable", bookId);
            book.ProcessingError = ex.Message;
            await SetStatusAsync(book, ProcessingStatus.Failed, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // An unparseable composition already degrades to a deterministic merge, so an
            // exception escaping here is a setup/persistence error. Land the book on Failed
            // with the reason rather than wedging it in "BuildingMemorySnapshots" after
            // Hangfire exhausts its retries.
            logger.LogError(ex, "BookMemorySnapshotJob failed for book {BookId}: unexpected error", bookId);
            book.ProcessingError = ex.Message;
            await SetStatusAsync(book, ProcessingStatus.Failed, CancellationToken.None);
        }
    }

    private async Task BuildAllAsync(Book book, CancellationToken ct)
    {
        var summaries = await db.SectionSummaries
            .Where(s => s.BookId == book.Id)
            .OrderBy(s => s.SectionIndex)
            .ToListAsync(ct);

        var endBySection = (await db.BookParagraphs
                .Where(p => p.BookId == book.Id)
                .GroupBy(p => p.SectionIndex)
                .Select(g => new { Section = g.Key, End = g.Max(p => p.LocationEnd) })
                .ToListAsync(ct))
            .ToDictionary(x => x.Section, x => x.End);

        var instruction = settings.GetString("prompts.bookMemory");
        var previous = new Memory(string.Empty, [], []);
        var built = 0;

        foreach (var summary in summaries)
        {
            previous = await ComposeAsync(book, instruction, previous, summary, ct);
            db.BookMemorySnapshots.Add(new BookMemorySnapshot
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                UpToSection = summary.SectionIndex,
                UpToLocation = endBySection.GetValueOrDefault(summary.SectionIndex, 0),
                SummarySoFar = previous.Summary,
                Timeline = previous.Timeline,
                OpenThreads = previous.OpenThreads,
                GeneratedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            built++;
        }

        logger.LogInformation("Built {Count} memory snapshot(s) for book {BookId}", built, book.Id);
    }

    private async Task<Memory> ComposeAsync(
        Book book, string instruction, Memory previous, SectionSummary summary, CancellationToken ct)
    {
        var sectionText = RenderSectionSummary(summary);

        var first = await CompleteAsync(book, summary.SectionIndex, instruction, previous.Summary, sectionText, false, ct);
        var parsed = LooseJsonParser.Parse<BookMemory>(first);
        if (HasSummary(parsed))
        {
            return Normalize(parsed);
        }

        logger.LogWarning(
            "BookMemorySnapshotJob: section {Index} of book {BookId} memory unparseable; retrying strict",
            summary.SectionIndex, book.Id);

        var second = await CompleteAsync(book, summary.SectionIndex, instruction, previous.Summary, sectionText, true, ct);
        parsed = LooseJsonParser.Parse<BookMemory>(second);
        if (HasSummary(parsed))
        {
            return Normalize(parsed);
        }

        logger.LogWarning(
            "BookMemorySnapshotJob: section {Index} of book {BookId} memory still unparseable; degrading",
            summary.SectionIndex, book.Id);
        return Degrade(previous, summary);
    }

    private async Task<string> CompleteAsync(
        Book book,
        int sectionIndex,
        string instruction,
        string previousSummary,
        string sectionText,
        bool strict,
        CancellationToken ct)
    {
        var existing = previousSummary.Length > 0 ? previousSummary : "(none yet)";
        var strictNote = strict
            ? "\n\nIMPORTANT: Respond with a single valid JSON object only — no markdown fences, no commentary."
            : string.Empty;
        var prompt = instruction + strictNote
            + "\n\nEXISTING MEMORY:\n" + existing
            + string.Create(CultureInfo.InvariantCulture, $"\n\nLATEST SECTION SUMMARY (section {sectionIndex}):\n")
            + sectionText;

        var messages = new[] { new ChatMessage { Role = ChatRole.User, Content = prompt } };
        var completion = await router.CompleteAsync(
            "memory",
            messages,
            new LlmCallMetadata { BookId = book.Id, SectionIndex = sectionIndex },
            ct);

        return completion.Content;
    }

    private static string RenderSectionSummary(SectionSummary summary)
    {
        var sb = new StringBuilder(summary.Summary);
        if (summary.ImportantEvents.Count > 0)
        {
            sb.Append("\nEvents:");
            foreach (var ev in summary.ImportantEvents)
            {
                sb.Append("\n- ").Append(ev.Description);
            }
        }

        if (summary.OpenThreads.Count > 0)
        {
            sb.Append("\nOpen threads:");
            foreach (var thread in summary.OpenThreads)
            {
                sb.Append("\n- ").Append(thread);
            }
        }

        return sb.ToString();
    }

    private static bool HasSummary(BookMemory? memory) => !string.IsNullOrWhiteSpace(memory?.SummarySoFar);

    private static Memory Normalize(BookMemory? memory) => new(
        (memory?.SummarySoFar ?? string.Empty).Trim(),
        Clean(memory?.Timeline),
        Clean(memory?.OpenThreads));

    private static Memory Degrade(Memory previous, SectionSummary summary)
    {
        var combined = previous.Summary.Length == 0
            ? summary.Summary
            : previous.Summary + "\n\n" + summary.Summary;
        var timeline = previous.Timeline
            .Concat(summary.ImportantEvents.Select(e => e.Description))
            .Where(t => t.Length > 0)
            .ToList();
        return new Memory(combined.Trim(), timeline, Clean(summary.OpenThreads));
    }

    private static List<string> Clean(IReadOnlyList<string>? values) =>
        (values ?? [])
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .ToList();

    private async Task SetStatusAsync(Book book, ProcessingStatus status, CancellationToken ct)
    {
        book.ProcessingStatus = status;
        if (status != ProcessingStatus.Failed)
        {
            book.ProcessingError = null;
        }

        book.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private sealed record Memory(string Summary, List<string> Timeline, List<string> OpenThreads);
}

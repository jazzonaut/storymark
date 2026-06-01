using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Retrieval;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Assistant;

/// <summary>Answers a free-text question at a reading position (docs/plan.md §18).
/// It composes the two existing chokepoints and adds no new ones:
/// <see cref="SpoilerSafeRetrievalService"/> is the only evidence path (so the
/// model can never be handed future content) and <see cref="ChatModelRouter"/> →
/// <c>LlmCallRecorder</c> is the only call path (so every turn is audited). The
/// answer turn is also persisted to <c>question_answers</c>.</summary>
internal sealed class AssistantService(
    StorymarkDbContext db,
    SpoilerSafeRetrievalService retrieval,
    ChatModelRouter router,
    ISettingsService settings)
{
    private const int PerQueryK = 8;
    private const int SnippetMaxChars = 240;

    // A chunk scores up to ~2.0 only when it surfaces in both the semantic and the
    // keyword pass (see SpoilerSafeRetrievalService.Rerank); that double hit is the
    // signal we treat as high confidence.
    private const double HighConfidenceScore = 1.5;

    /// <summary>Returns the spoiler-safe answer, or <c>null</c> if the book does not
    /// exist (the caller maps that to 404).</summary>
    public async Task<AssistantAnswer?> AnswerAsync(
        Guid bookId,
        string question,
        int currentSection,
        int currentLocation,
        CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        var maxEvidence = await ResolveMaxEvidenceAsync(ct);
        var evidence = await GatherEvidenceAsync(
            bookId, question, currentSection, currentLocation, maxEvidence, ct);

        // Always false because the SQL filter already excluded future content; deriving
        // it from the evidence (rather than hardcoding false) makes the audit flag fire
        // if that filter ever regresses (CLAUDE.md, docs/plan.md §17).
        var usedFutureContent = evidence.Any(e =>
            e.SectionIndex > currentSection
            || (e.SectionIndex == currentSection && e.LocationEnd > currentLocation));

        var prompt = RenderPrompt(question, currentSection, currentLocation, evidence);
        var messages = new[] { new ChatMessage { Role = ChatRole.User, Content = prompt } };
        var metadata = new LlmCallMetadata
        {
            BookId = bookId,
            SectionIndex = currentSection,
            UsedFutureContent = usedFutureContent,
        };

        var completion = await router.CompleteAsync("qa", messages, metadata, ct);
        var answer = completion.Content.Trim();

        var sources = evidence
            .Select(e => new QuestionSource
            {
                SectionIndex = e.SectionIndex,
                LocationStart = e.LocationStart,
                LocationEnd = e.LocationEnd,
                Snippet = Snippet(e.Text),
            })
            .ToList();

        db.QuestionAnswers.Add(new QuestionAnswer
        {
            Id = Guid.NewGuid(),
            BookId = bookId,
            Question = question,
            Answer = answer,
            CurrentSection = currentSection,
            CurrentLocation = currentLocation,
            Sources = sources,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return new AssistantAnswer
        {
            Answer = answer,
            Confidence = Confidence(evidence),
            Sources = sources,
            UsedFutureContent = usedFutureContent,
        };
    }

    /// <summary>Runs every expanded query through the spoiler-safe path, then merges:
    /// de-dup by <c>(section, chunk)</c> keeping the strongest hit, and cap to the top
    /// evidence by score. Scores are per-query and not strictly comparable across them,
    /// so the merge is a pragmatic best-of rather than a fused ranking.</summary>
    /// <summary>Caps the evidence set: the full <c>assistant.evidence.maxChunks</c>,
    /// or the smaller <c>maxChunksLocal</c> when <c>trimLocal</c> is on and the qa
    /// model is local (Ollama) — a shorter prompt for slow CPU inference. Hosted
    /// models always get the full set.</summary>
    private async Task<int> ResolveMaxEvidenceAsync(CancellationToken ct)
    {
        var max = settings.GetInt("assistant.evidence.maxChunks");
        if (!settings.GetBool("assistant.evidence.trimLocal"))
        {
            return max;
        }

        var kind = await router.ResolveTaskKindAsync("qa", ct);
        return kind == LlmProviderKind.Ollama
            ? settings.GetInt("assistant.evidence.maxChunksLocal")
            : max;
    }

    private async Task<List<EvidenceChunk>> GatherEvidenceAsync(
        Guid bookId,
        string question,
        int currentSection,
        int currentLocation,
        int maxEvidence,
        CancellationToken ct)
    {
        var synonyms = settings.GetJson<Dictionary<string, List<string>>>("search.synonyms");
        var queries = QueryExpander.BuildQueries(question, synonyms);

        var collected = new List<EvidenceChunk>();
        foreach (var query in queries)
        {
            collected.AddRange(
                await retrieval.SearchAsync(bookId, query, currentSection, currentLocation, PerQueryK, ct));
        }

        return collected
            .GroupBy(e => (e.SectionIndex, e.ChunkIndex))
            .Select(g => g.OrderByDescending(e => e.Score).First())
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.SectionIndex)
            .ThenBy(e => e.ChunkIndex)
            .Take(maxEvidence)
            .ToList();
    }

    private string RenderPrompt(
        string question,
        int currentSection,
        int currentLocation,
        List<EvidenceChunk> evidence)
    {
        var context = new StringBuilder();
        foreach (var chunk in evidence)
        {
            if (context.Length > 0)
            {
                context.Append("\n\n");
            }

            context.Append(CultureInfo.InvariantCulture, $"[Section {chunk.SectionIndex}]\n{chunk.Text}");
        }

        return settings.GetString("prompts.qa")
            .Replace("{currentSection}", currentSection.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{currentLocation}", currentLocation.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{question}", question, StringComparison.Ordinal)
            .Replace("{context}", context.ToString(), StringComparison.Ordinal);
    }

    private static AnswerConfidence Confidence(List<EvidenceChunk> evidence)
    {
        if (evidence.Count == 0)
        {
            return AnswerConfidence.Low;
        }

        return evidence.Max(e => e.Score) >= HighConfidenceScore
            ? AnswerConfidence.High
            : AnswerConfidence.Medium;
    }

    private static string Snippet(string text)
    {
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= SnippetMaxChars ? flat : flat[..SnippetMaxChars].TrimEnd() + "…";
    }
}

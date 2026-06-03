using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Text;
using Storymark.Api.Prompts;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Retrieval;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Assistant;

/// <summary>Answers a free-text question at a reading position (docs/plan.md §18).
/// It composes the two existing chokepoints and adds no new ones:
/// <see cref="SpoilerSafeRetrievalService"/> is the only evidence path (retrieval
/// can never return future content) and <see cref="ChatModelRouter"/> →
/// <c>LlmCallRecorder</c> is the only call path (so every turn is audited). The
/// prompt additionally carries client-attested text — the question, and the
/// optional selection/history conversation context — which is position-gated in
/// the client and only clamped here; docs/adr/0021 records why that is accepted
/// on an anonymous API whose position parameters are client-attested everywhere.
/// The answer turn is also persisted to <c>question_answers</c>.</summary>
internal sealed class AssistantService(
    StorymarkDbContext db,
    SpoilerSafeRetrievalService retrieval,
    ChatModelRouter router,
    ISettingsService settings,
    ILogger<AssistantService> logger)
{
    private const int PerQueryK = 8;
    private const int SnippetMaxChars = 240;
    private const int MaxSelectionChars = 500;
    private const int MaxHistoryTurns = 6;
    private const int MaxHistoryTurnChars = 2000;

    // Matches a bare numeric citation marker like [2], for mapping an answer's
    // citations back to evidence. The context labels its passage numbers the same
    // way and tells the model to cite with them; the "(Section N)" label uses
    // parens so it never collides with this.
    private static readonly Regex CitationMarker = new(
        @"\[(?<n>\d+)\]",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    // Strips citation markers from replayed history answers — their numbers point
    // at a previous turn's evidence set, so kept verbatim they would teach the
    // model to cite passages that don't exist in this turn. Also eats the spaces
    // before a marker, but never a newline: stripping a line-leading marker must
    // not join two paragraphs.
    private static readonly Regex HistoryCitationMarker = new(
        @"[^\S\n]*\[\d+\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    // A chunk scores up to ~2.0 only when it surfaces in both the semantic and the
    // keyword pass (see SpoilerSafeRetrievalService.Rerank); that double hit is the
    // signal we treat as high confidence.
    private const double HighConfidenceScore = 1.5;

    /// <summary>Returns the spoiler-safe answer, or <c>null</c> if the book does not
    /// exist (the caller maps that to 404).</summary>
    public async Task<AssistantAnswer?> AnswerAsync(
        Guid bookId,
        AssistantQuestion request,
        CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        var question = request.Question;
        var currentSection = request.CurrentSection;
        var currentLocation = request.CurrentLocation;
        var selectedText = NormalizeSelection(request.SelectedText);
        var history = NormalizeHistory(request.History);

        var maxEvidence = await ResolveMaxEvidenceAsync(ct);
        var evidence = await GatherEvidenceAsync(
            bookId, question, selectedText, currentSection, currentLocation, maxEvidence, ct);

        // Always false because the SQL filter already excluded future content; deriving
        // it from the evidence (rather than hardcoding false) makes the audit flag fire
        // if that filter ever regresses (CLAUDE.md, docs/plan.md §17). The flag covers
        // retrieved evidence only: the carried selection/history is client-attested and
        // never feeds it (docs/adr/0021).
        var usedFutureContent = evidence.Any(e =>
            SpoilerBoundary.IsBeyond(e.SectionIndex, e.LocationEnd, currentSection, currentLocation));

        var prompt = RenderPrompt(question, selectedText, history, currentSection, currentLocation, evidence);
        var messages = new[] { new ChatMessage { Role = ChatRole.User, Content = prompt } };
        var metadata = new LlmCallMetadata
        {
            BookId = bookId,
            SectionIndex = currentSection,
            UsedFutureContent = usedFutureContent,
        };

        var completion = await router.CompleteAsync("qa", messages, metadata, ct);
        var answer = completion.Content.Trim();

        var sources = BuildSources(answer, evidence);

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

    // The clamps below bound the prompt: the endpoint is public and the client
    // carries the conversation context, so an oversized selection or history must
    // shrink to a bounded prompt instead of overwhelming a (possibly local) model.
    private static string? NormalizeSelection(string? selectedText)
    {
        var trimmed = selectedText?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return Ellipsis.Truncate(trimmed, MaxSelectionChars);
    }

    private static List<AssistantHistoryTurn> NormalizeHistory(IReadOnlyList<AssistantHistoryTurn> history)
    {
        return history
            .Where(t => !string.IsNullOrWhiteSpace(t.Question) && !string.IsNullOrWhiteSpace(t.Answer))
            .TakeLast(MaxHistoryTurns)
            .Select(t => new AssistantHistoryTurn
            {
                Question = Ellipsis.Truncate(t.Question.Trim(), MaxHistoryTurnChars),
                Answer = Ellipsis.Truncate(
                    HistoryCitationMarker.Replace(t.Answer, string.Empty).Trim(), MaxHistoryTurnChars),
            })
            .ToList();
    }

    /// <summary>Runs every expanded query through the spoiler-safe path, then merges:
    /// de-dup by <c>(section, chunk)</c> keeping the strongest hit, and cap to the top
    /// evidence by score. Scores are per-query and not strictly comparable across them,
    /// so the merge is a pragmatic best-of rather than a fused ranking.</summary>
    private async Task<List<EvidenceChunk>> GatherEvidenceAsync(
        Guid bookId,
        string question,
        string? selectedText,
        int currentSection,
        int currentLocation,
        int maxEvidence,
        CancellationToken ct)
    {
        var synonyms = settings.GetJson<Dictionary<string, List<string>>>("search.synonyms");
        var queries = QueryExpander.BuildQueries(question, synonyms).ToList();
        // The selected passage is the strongest retrieval signal for "what is this?"
        // style questions, so it runs as its own query alongside the expanded set.
        if (selectedText is not null
            && !queries.Contains(selectedText, StringComparer.OrdinalIgnoreCase))
        {
            queries.Add(selectedText);
        }

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
        string? selectedText,
        List<AssistantHistoryTurn> history,
        int currentSection,
        int currentLocation,
        List<EvidenceChunk> evidence)
    {
        var context = new StringBuilder();
        for (var i = 0; i < evidence.Count; i++)
        {
            if (context.Length > 0)
            {
                context.Append("\n\n");
            }

            var chunk = evidence[i];
            context.Append(
                CultureInfo.InvariantCulture,
                $"[{i + 1}] (Section {chunk.SectionIndex})\n{chunk.Text}");
        }

        // The blocks carry their own labels so an absent selection or history leaves
        // no dangling heading in the rendered prompt — the slot just goes blank.
        var selection = selectedText is null
            ? string.Empty
            : $"The reader selected this passage and is asking about it:\n\"{selectedText}\"\n";

        var conversation = new StringBuilder();
        if (history.Count > 0)
        {
            conversation.Append("Conversation so far:\n");
            foreach (var turn in history)
            {
                conversation.Append(CultureInfo.InvariantCulture, $"Q: {turn.Question}\nA: {turn.Answer}\n");
            }
        }

        return PromptContract.Resolve("prompts.qa", settings, logger, PromptDefaults.Qa)
            .Replace("{currentSection}", currentSection.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{currentLocation}", currentLocation.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{selection}", selection, StringComparison.Ordinal)
            .Replace("{history}", conversation.ToString(), StringComparison.Ordinal)
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

    /// <summary>Maps the answer's inline <c>[n]</c> markers back to the numbered
    /// evidence so the UI shows only what the answer actually leaned on, in the
    /// order it was first cited. When the model cites nothing (common on weaker
    /// local models), falls back to the full evidence set — the UI presents that
    /// as "related passages" rather than citations.</summary>
    private static List<QuestionSource> BuildSources(string answer, List<EvidenceChunk> evidence)
    {
        var cited = ParseCitations(answer, evidence.Count);
        var numbered = cited.Count > 0
            ? cited.Select(n => (Index: n, Chunk: evidence[n - 1]))
            : evidence.Select((chunk, i) => (Index: i + 1, Chunk: chunk));

        return numbered
            .Select(x => new QuestionSource
            {
                Index = x.Index,
                SectionIndex = x.Chunk.SectionIndex,
                LocationStart = x.Chunk.LocationStart,
                LocationEnd = x.Chunk.LocationEnd,
                Snippet = Snippet(x.Chunk.Text),
            })
            .ToList();
    }

    private static List<int> ParseCitations(string answer, int evidenceCount)
    {
        var seen = new List<int>();
        foreach (Match match in CitationMarker.Matches(answer))
        {
            if (int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                && n >= 1
                && n <= evidenceCount
                && !seen.Contains(n))
            {
                seen.Add(n);
            }
        }

        return seen;
    }

    private static string Snippet(string text) =>
        Ellipsis.Truncate(text.ReplaceLineEndings(" ").Trim(), SnippetMaxChars);
}

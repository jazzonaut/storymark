using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Text;
using Storymark.Api.Prompts;
using Storymark.Api.Services.Extraction;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Retrieval;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Books;

/// <summary>Builds the Current Scene card from already-read context only. This is a
/// local-position surface rather than search: the strongest signal is the recent
/// paragraphs immediately behind the reader, with recent events and memory used as
/// supporting context. The LLM call still goes through <see cref="ChatModelRouter"/>,
/// so it is model-routed and audited like Q&amp;A.</summary>
internal sealed class CurrentSceneService(
    StorymarkDbContext db,
    BookMemoryService memory,
    ChatModelRouter router,
    ISettingsService settings,
    ILogger<CurrentSceneService> logger)
{
    private const int MaxPassageChars = 4500;
    private const int MaxParagraphRows = 24;
    private const int MaxEventCount = 6;
    private const int SourceCount = 3;
    private const int SourceSnippetChars = 220;

    public async Task<CurrentSceneView?> GetAsync(
        Guid bookId,
        Guid userId,
        int currentSection,
        int currentLocation,
        CancellationToken ct)
    {
        if (!await db.Books.IsVisibleAsync(bookId, userId, ct))
        {
            return null;
        }

        var paragraphs = await GetRecentParagraphsAsync(bookId, currentSection, currentLocation, ct);
        var passage = RenderPassage(paragraphs);
        if (string.IsNullOrWhiteSpace(passage))
        {
            return EmptyScene();
        }

        var latestMemory = await memory.GetLatestAsync(bookId, currentSection, currentLocation, ct);
        var recentEvents = await GetRecentEventsAsync(bookId, currentSection, currentLocation, ct);
        var prompt = RenderPrompt(currentSection, currentLocation, passage, latestMemory, recentEvents);

        var usedFutureContent = paragraphs.Any(p =>
                SpoilerBoundary.IsBeyond(p.SectionIndex, p.LocationEnd, currentSection, currentLocation))
            || recentEvents.Any(e =>
                SpoilerBoundary.IsBeyond(e.SectionIndex, e.LocationEnd, currentSection, currentLocation));

        // Tolerant per ADR-0009: a small model sometimes answers in markdown prose
        // instead of JSON. Retry once with a stricter instruction before degrading.
        var parsed = await CompleteAndParseAsync(bookId, currentSection, prompt, strict: false, usedFutureContent, ct);
        if (!HasSummary(parsed))
        {
            parsed = await CompleteAndParseAsync(bookId, currentSection, prompt, strict: true, usedFutureContent, ct);
        }

        if (!HasSummary(parsed))
        {
            logger.LogWarning(
                "Current scene model response had no usable summary after a strict retry for book {BookId} section {Section}",
                bookId,
                currentSection);
        }

        var entityIds = await ResolveEntityIdsAsync(bookId, currentSection, currentLocation, ct);
        return ToView(parsed, entityIds, BuildSources(paragraphs));
    }

    private async Task<GeneratedCurrentScene?> CompleteAndParseAsync(
        Guid bookId,
        int currentSection,
        string basePrompt,
        bool strict,
        bool usedFutureContent,
        CancellationToken ct)
    {
        var prompt = strict
            ? basePrompt
                + "\n\nIMPORTANT: Respond with a single valid JSON object only — no markdown, no bullet points, no commentary before or after the JSON."
            : basePrompt;

        var completion = await router.CompleteAsync(
            "current_scene",
            [new ChatMessage { Role = ChatRole.User, Content = prompt }],
            new LlmCallMetadata
            {
                BookId = bookId,
                SectionIndex = currentSection,
                UsedFutureContent = usedFutureContent,
            },
            ct);

        return LooseJsonParser.Parse<GeneratedCurrentScene>(completion.Content);
    }

    private static bool HasSummary(GeneratedCurrentScene? parsed) => Clean(parsed?.Summary) is not null;

    private async Task<List<SceneParagraph>> GetRecentParagraphsAsync(
        Guid bookId,
        int currentSection,
        int currentLocation,
        CancellationToken ct)
    {
        var rows = await db.BookParagraphs.AsNoTracking()
            .Where(p => p.BookId == bookId
                && (p.SectionIndex < currentSection
                    || (p.SectionIndex == currentSection && p.LocationEnd <= currentLocation)))
            .OrderByDescending(p => p.SectionIndex)
            .ThenByDescending(p => p.ParagraphIndex)
            .Select(p => new SceneParagraph
            {
                SectionIndex = p.SectionIndex,
                ParagraphIndex = p.ParagraphIndex,
                LocationStart = p.LocationStart,
                LocationEnd = p.LocationEnd,
                Text = p.PlainText,
            })
            .Take(MaxParagraphRows)
            .ToListAsync(ct);

        var selected = new List<SceneParagraph>();
        var chars = 0;
        foreach (var row in rows)
        {
            if (selected.Count > 0 && chars + row.Text.Length > MaxPassageChars)
            {
                break;
            }

            selected.Add(row);
            chars += row.Text.Length;
        }

        selected.Reverse();
        return selected;
    }

    private async Task<List<EventView>> GetRecentEventsAsync(
        Guid bookId,
        int currentSection,
        int currentLocation,
        CancellationToken ct)
    {
        var events = await db.BookEvents.AsNoTracking()
            .Where(e => e.BookId == bookId
                && (e.SectionIndex < currentSection
                    || (e.SectionIndex == currentSection && e.LocationEnd <= currentLocation)))
            .OrderByDescending(e => e.SectionIndex)
            .ThenByDescending(e => e.LocationStart)
            .Take(MaxEventCount)
            .Select(e => new EventView
            {
                SectionIndex = e.SectionIndex,
                LocationStart = e.LocationStart,
                LocationEnd = e.LocationEnd,
                EventType = e.EventType,
                Description = e.Description,
                InvolvedEntityIds = e.InvolvedEntityIds,
            })
            .ToListAsync(ct);

        events.Reverse();
        return events;
    }

    private async Task<Dictionary<string, Guid>> ResolveEntityIdsAsync(
        Guid bookId,
        int currentSection,
        int currentLocation,
        CancellationToken ct)
    {
        var entities = await db.BookEntities.AsNoTracking()
            .Where(e => e.BookId == bookId
                && e.FirstSeenSection != null
                && (e.FirstSeenSection < currentSection
                    || (e.FirstSeenSection == currentSection && (e.FirstSeenLocation ?? 0) <= currentLocation)))
            .Select(e => new { e.Id, e.CanonicalName, e.Aliases })
            .ToListAsync(ct);

        var map = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            AddEntityKey(map, entity.CanonicalName, entity.Id);
            foreach (var alias in entity.Aliases)
            {
                AddEntityKey(map, alias, entity.Id);
            }
        }

        return map;
    }

    private static void AddEntityKey(Dictionary<string, Guid> map, string name, Guid id)
    {
        var key = EntityNameNormalizer.MatchKey(name);
        if (key.Length > 0)
        {
            map.TryAdd(key, id);
        }
    }

    private string RenderPrompt(
        int currentSection,
        int currentLocation,
        string passage,
        MemoryView? latestMemory,
        List<EventView> recentEvents)
    {
        var eventsText = recentEvents.Count == 0
            ? "None."
            : string.Join(
                "\n",
                recentEvents.Select(e =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Section {e.SectionIndex}: {e.Description}")));

        var memoryText = latestMemory is null
            ? "No story-so-far memory is available yet."
            : Ellipsis.Truncate(latestMemory.SummarySoFar, 1200);
        var threads = latestMemory is null || latestMemory.OpenThreads.Count == 0
            ? "None."
            : string.Join("\n", latestMemory.OpenThreads.Select(t => $"- {t}"));

        return PromptContract.Resolve("prompts.currentScene", settings, logger, PromptDefaults.CurrentScene)
            .Replace("{currentSection}", currentSection.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{currentLocation}", currentLocation.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{recentPassage}", passage, StringComparison.Ordinal)
            .Replace("{recentEvents}", eventsText, StringComparison.Ordinal)
            .Replace("{memory}", memoryText, StringComparison.Ordinal)
            .Replace("{openThreads}", threads, StringComparison.Ordinal);
    }

    private static string RenderPassage(IReadOnlyList<SceneParagraph> paragraphs)
    {
        var sb = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph.Text))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append("\n\n");
            }

            sb.Append(
                CultureInfo.InvariantCulture,
                $"[Section {paragraph.SectionIndex}, paragraph {paragraph.ParagraphIndex}]\n{paragraph.Text.Trim()}");
        }

        return sb.ToString();
    }

    private static CurrentSceneView ToView(
        GeneratedCurrentScene? parsed,
        IReadOnlyDictionary<string, Guid> entityIds,
        IReadOnlyList<QuestionSource> sources)
    {
        // Degrade so the card always shows something honest: never the raw model
        // output, which after two failed parses is likely broken JSON or prose
        // scaffolding. The sources below still anchor the reader to the passage.
        var summary = Clean(parsed?.Summary)
            ?? "The current scene couldn't be summarised just now. The recent passage below shows where you are.";

        return new CurrentSceneView
        {
            Summary = summary,
            PresentEntities = (parsed?.PresentEntities ?? [])
                .Select(e => ToEntityView(e, entityIds))
                .Where(e => e is not null)
                .Select(e => e!)
                .Take(6)
                .ToList(),
            Place = Clean(parsed?.Place),
            ImmediateTension = Clean(parsed?.ImmediateTension),
            RelevantThreads = CleanList(parsed?.RelevantThreads, 5),
            RecentChange = Clean(parsed?.RecentChange),
            SuggestedQuestions = CleanList(parsed?.SuggestedQuestions, 3),
            Sources = sources,
        };
    }

    private static CurrentSceneEntityView? ToEntityView(
        GeneratedCurrentSceneEntity entity,
        IReadOnlyDictionary<string, Guid> entityIds)
    {
        var name = Clean(entity.Name);
        var role = Clean(entity.Role);
        if (name is null || role is null)
        {
            return null;
        }

        entityIds.TryGetValue(EntityNameNormalizer.MatchKey(name), out var id);
        return new CurrentSceneEntityView
        {
            EntityId = id == Guid.Empty ? null : id,
            Name = name,
            Role = role,
        };
    }

    private static List<string> CleanList(IReadOnlyList<string>? items, int max) =>
        (items ?? [])
            .Select(Clean)
            .Where(item => item is not null)
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }

    private static List<QuestionSource> BuildSources(IReadOnlyList<SceneParagraph> paragraphs) =>
        paragraphs
            .TakeLast(SourceCount)
            .Select((paragraph, index) => new QuestionSource
            {
                Index = index + 1,
                SectionIndex = paragraph.SectionIndex,
                LocationStart = paragraph.LocationStart,
                LocationEnd = paragraph.LocationEnd,
                Snippet = Ellipsis.Truncate(paragraph.Text.ReplaceLineEndings(" ").Trim(), SourceSnippetChars),
            })
            .ToList();

    private static CurrentSceneView EmptyScene() => new()
    {
        Summary = "There is not enough read text at this position to describe the current scene yet.",
        PresentEntities = [],
        Place = null,
        ImmediateTension = null,
        RelevantThreads = [],
        RecentChange = null,
        SuggestedQuestions = ["What happened just before this?"],
        Sources = [],
    };

    private sealed record SceneParagraph
    {
        public required int SectionIndex { get; init; }

        public required int ParagraphIndex { get; init; }

        public required int LocationStart { get; init; }

        public required int LocationEnd { get; init; }

        public required string Text { get; init; }
    }

    private sealed record GeneratedCurrentScene
    {
        public string? Summary { get; init; }

        public IReadOnlyList<GeneratedCurrentSceneEntity>? PresentEntities { get; init; }

        public string? Place { get; init; }

        public string? ImmediateTension { get; init; }

        public IReadOnlyList<string>? RelevantThreads { get; init; }

        public string? RecentChange { get; init; }

        public IReadOnlyList<string>? SuggestedQuestions { get; init; }
    }

    private sealed record GeneratedCurrentSceneEntity
    {
        public string? Name { get; init; }

        public string? Role { get; init; }
    }
}

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Books;
using Storymark.Api.Services.Extraction;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Retrieval;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Entities;

/// <summary>Lazy, spoiler-safe entity summaries (docs/plan.md §16.3). On the first tap
/// of an entity at a reading position it generates a short summary from only the content
/// the reader has reached and caches it; later taps at that position reuse the row. Like
/// <see cref="Assistant.AssistantService"/> it composes the two invariant chokepoints —
/// <see cref="SpoilerSafeRetrievalService"/> for evidence and <see cref="ChatModelRouter"/>
/// → the recorder for the call — and adds no new evidence or call path.
///
/// <para>This is a synchronous service rather than the Hangfire <c>EntitySummaryJob</c>
/// the plan sketches: the endpoint must return the generated summary, so it awaits
/// generation inline (a fire-and-forget job can't return a value, and a never-enqueued
/// job would be dead code). Generation costs one <c>entity_summary</c> chat call plus the
/// query embed retrieval records.</para></summary>
internal sealed class EntitySummaryService(
    StorymarkDbContext db,
    SpoilerSafeRetrievalService retrieval,
    ChatModelRouter router,
    ISettingsService settings)
{
    private const int EvidenceK = 6;
    private const int MaxMentions = 8;

    /// <summary>Returns the cached-or-generated summary, or <c>null</c> if the book or
    /// entity is unknown, or the entity is not visible at the position (the caller maps
    /// that to 404 — you can't summarise something you haven't met).</summary>
    public async Task<EntitySummaryView?> GetOrCreateAsync(
        Guid bookId, Guid entityId, int section, int location, CancellationToken ct)
    {
        var entity = await db.BookEntities.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entityId && e.BookId == bookId, ct);
        if (entity is null || !IsVisible(entity, section, location))
        {
            return null;
        }

        var cached = await GetCachedAsync(entityId, section, location, ct);
        if (cached is not null)
        {
            return ToView(entity, cached.ShortSummary, cached.KnownFacts, cached.Relationships, cached.OpenQuestions);
        }

        return await GenerateAsync(entity, section, location, ct);
    }

    // Reuse a summary generated earlier in the *same* section (it's spoiler-safe and
    // "fresh enough", docs/plan.md §16.3). Crossing into a new section deliberately
    // misses, so the next tap regenerates a richer summary covering the new content.
    private async Task<BookEntitySummary?> GetCachedAsync(
        Guid entityId, int section, int location, CancellationToken ct) =>
        await db.BookEntitySummaries.AsNoTracking()
            .Where(s => s.EntityId == entityId
                && s.UpToSection == section
                && s.UpToLocation <= location)
            .OrderByDescending(s => s.UpToLocation)
            .FirstOrDefaultAsync(ct);

    private async Task<EntitySummaryView> GenerateAsync(
        BookEntity entity, int section, int location, CancellationToken ct)
    {
        var mentions = await db.BookEntityMentions.AsNoTracking()
            .Where(m => m.EntityId == entity.Id
                && (m.SectionIndex < section
                    || (m.SectionIndex == section && m.LocationEnd <= location)))
            .OrderBy(m => m.SectionIndex)
            .ThenBy(m => m.LocationStart)
            .Select(m => m.SurroundingText)
            .ToListAsync(ct);

        var evidence = await retrieval.SearchAsync(entity.BookId, entity.CanonicalName, section, location, EvidenceK, ct);

        // Always false: the retrieval SQL already excluded future content. Derived (not
        // hardcoded) so the audit flag fires if that filter ever regresses.
        var usedFutureContent = evidence.Any(e =>
            e.SectionIndex > section
            || (e.SectionIndex == section && e.LocationEnd > location));

        var prompt = RenderPrompt(entity, mentions, evidence);

        // Tolerant per ADR-0009: a small model sometimes answers in markdown prose
        // instead of JSON. Retry once with a stricter instruction before degrading.
        var parsed = await CompleteAndParseAsync(entity, section, prompt, strict: false, usedFutureContent, ct);
        if (!HasSummary(parsed))
        {
            parsed = await CompleteAndParseAsync(entity, section, prompt, strict: true, usedFutureContent, ct);
        }

        var (shortSummary, knownFacts, relationships, openQuestions) = Normalize(parsed, entity, mentions);

        db.BookEntitySummaries.Add(new BookEntitySummary
        {
            Id = Guid.NewGuid(),
            BookId = entity.BookId,
            EntityId = entity.Id,
            UpToSection = section,
            UpToLocation = location,
            ShortSummary = shortSummary,
            KnownFacts = knownFacts,
            Relationships = relationships,
            OpenQuestions = openQuestions,
            GeneratedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return ToView(entity, shortSummary, knownFacts, relationships, openQuestions);
    }

    private async Task<EntitySummaryDoc?> CompleteAndParseAsync(
        BookEntity entity, int section, string basePrompt, bool strict, bool usedFutureContent, CancellationToken ct)
    {
        var prompt = strict
            ? basePrompt
                + "\n\nIMPORTANT: Respond with a single valid JSON object only — no markdown, no bullet points, no commentary before or after the JSON."
            : basePrompt;

        var messages = new[] { new ChatMessage { Role = ChatRole.User, Content = prompt } };
        var completion = await router.CompleteAsync(
            "entity_summary",
            messages,
            new LlmCallMetadata
            {
                BookId = entity.BookId,
                SectionIndex = section,
                UsedFutureContent = usedFutureContent,
            },
            ct);

        return LooseJsonParser.Parse<EntitySummaryDoc>(completion.Content);
    }

    private static bool HasSummary(EntitySummaryDoc? doc) => !string.IsNullOrWhiteSpace(doc?.ShortSummary);

    private string RenderPrompt(
        BookEntity entity, IReadOnlyList<string?> mentions, IReadOnlyList<EvidenceChunk> evidence)
    {
        var sb = new StringBuilder(settings.GetString("prompts.entitySummary"));
        sb.Append(CultureInfo.InvariantCulture, $"\n\nENTITY: {entity.CanonicalName} ({entity.EntityType})");
        if (entity.Aliases.Count > 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\nAlso known as: {string.Join(", ", entity.Aliases)}");
        }

        var mentionLines = mentions
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxMentions)
            .ToList();
        if (mentionLines.Count > 0)
        {
            sb.Append("\n\nMENTIONS:");
            foreach (var line in mentionLines)
            {
                sb.Append("\n- ").Append(line);
            }
        }

        sb.Append("\n\nCONTEXT:");
        if (evidence.Count == 0)
        {
            sb.Append("\n(none)");
        }
        else
        {
            foreach (var chunk in evidence)
            {
                sb.Append(CultureInfo.InvariantCulture, $"\n\n[Section {chunk.SectionIndex}]\n{chunk.Text}");
            }
        }

        return sb.ToString();
    }

    private static (string Summary, List<string> Facts, List<string> Relationships, List<string> Questions) Normalize(
        EntitySummaryDoc? doc, BookEntity entity, IReadOnlyList<string?> mentions)
    {
        var summary = doc?.ShortSummary?.Trim();
        if (string.IsNullOrWhiteSpace(summary))
        {
            // Degrade so the popup always shows something: the first mention snippet, or
            // a bare placeholder. Never block the reader on a weak model.
            summary = mentions.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))?.Trim()
                ?? $"{entity.CanonicalName} has been mentioned, but little is known yet.";
        }

        return (summary, Coerce(doc?.KnownFacts), Coerce(doc?.Relationships), Coerce(doc?.OpenQuestions));
    }

    // The small models return these lists as either plain strings or small objects
    // (e.g. {"type":"friend","subject":"Liora"}); accept both and render each item as
    // a display string so a shape wobble never blanks the whole summary.
    private static List<string> Coerce(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            var text = Stringify(item);
            if (text.Length > 0)
            {
                result.Add(text);
            }
        }

        return result;
    }

    private static string Stringify(JsonElement item) =>
        item.ValueKind switch
        {
            JsonValueKind.String => (item.GetString() ?? string.Empty).Trim(),
            JsonValueKind.Object => string.Join(
                " — ",
                item.EnumerateObject()
                    .Select(p => p.Value.ValueKind == JsonValueKind.String
                        ? p.Value.GetString()?.Trim()
                        : p.Value.ToString().Trim())
                    .Where(v => !string.IsNullOrWhiteSpace(v))),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => item.ToString(),
            _ => string.Empty,
        };

    private static EntitySummaryView ToView(
        BookEntity entity,
        string shortSummary,
        IReadOnlyList<string> knownFacts,
        IReadOnlyList<string> relationships,
        IReadOnlyList<string> openQuestions) =>
        new()
        {
            CanonicalName = entity.CanonicalName,
            EntityType = entity.EntityType.ToString(),
            ShortSummary = shortSummary,
            KnownFacts = knownFacts,
            Relationships = relationships,
            OpenQuestions = openQuestions,
        };

    private static bool IsVisible(BookEntity entity, int section, int location)
    {
        if (entity.FirstSeenSection is not int firstSeen)
        {
            return false;
        }

        return firstSeen < section
            || (firstSeen == section && (entity.FirstSeenLocation ?? 0) <= location);
    }

    /// <summary>The entity summary the model returns as JSON. The prompt asks for a
    /// <c>sources</c> field too; it has no §12.10 column, so it's ignored (like the
    /// extra fields in the book-memory doc). The lists are captured as raw
    /// <see cref="JsonElement"/> because small models render them as strings or as
    /// small objects interchangeably — <see cref="Coerce"/> handles both.</summary>
    private sealed record EntitySummaryDoc
    {
        public string? ShortSummary { get; init; }

        public JsonElement? KnownFacts { get; init; }

        public JsonElement? Relationships { get; init; }

        public JsonElement? OpenQuestions { get; init; }
    }
}

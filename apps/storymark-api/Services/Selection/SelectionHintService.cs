using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Retrieval;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Selection;

/// <summary>The tiered selection path (docs/plan.md §5.5): a known entity
/// (canonical name or alias, visible at the reading position) short-circuits to
/// a cached card with no LLM call; anything else falls back to a fast 1B hint
/// built from spoiler-safe evidence. Like <see cref="Assistant.AssistantService"/>
/// it composes the existing chokepoints (<see cref="SpoilerSafeRetrievalService"/>,
/// <see cref="ChatModelRouter"/>) and adds no new evidence or call path.</summary>
internal sealed class SelectionHintService(
    StorymarkDbContext db,
    SpoilerSafeRetrievalService retrieval,
    ChatModelRouter router,
    ISettingsService settings)
{
    private const int HintK = 4;

    /// <summary>Resolves a selection to an entity card or a generated hint, or
    /// <c>null</c> if the book does not exist (the caller maps that to 404).</summary>
    public async Task<SelectionHintResult?> GetHintAsync(
        Guid bookId,
        string selectedText,
        int currentSection,
        int currentLocation,
        CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        var entity = await MatchEntityAsync(bookId, selectedText, currentSection, currentLocation, ct);
        if (entity is not null)
        {
            return SelectionHintResult.ForEntity(
                entity.Id,
                new EntityCard
                {
                    CanonicalName = entity.CanonicalName,
                    EntityType = entity.EntityType.ToString(),
                    Aliases = entity.Aliases,
                    FirstSeenSection = entity.FirstSeenSection,
                });
        }

        return await BuildHintAsync(bookId, selectedText, currentSection, currentLocation, ct);
    }

    private async Task<BookEntity?> MatchEntityAsync(
        Guid bookId,
        string selectedText,
        int currentSection,
        int currentLocation,
        CancellationToken ct)
    {
        var needle = Normalize(selectedText);
        if (needle.Length == 0)
        {
            return null;
        }

        var entities = await db.BookEntities.AsNoTracking().Where(e => e.BookId == bookId).ToListAsync(ct);
        return entities.FirstOrDefault(e =>
            IsVisible(e, currentSection, currentLocation) && Matches(e, needle));
    }

    private async Task<SelectionHintResult> BuildHintAsync(
        Guid bookId,
        string selectedText,
        int currentSection,
        int currentLocation,
        CancellationToken ct)
    {
        var evidence = await retrieval.SearchAsync(bookId, selectedText, currentSection, currentLocation, HintK, ct);

        // Always false because the retrieval SQL already excluded future content;
        // derived (not hardcoded) so the audit flag fires on a filter regression.
        var usedFutureContent = evidence.Any(e =>
            e.SectionIndex > currentSection
            || (e.SectionIndex == currentSection && e.LocationEnd > currentLocation));

        var prompt = RenderPrompt(selectedText, currentSection, currentLocation, evidence);
        var messages = new[] { new ChatMessage { Role = ChatRole.User, Content = prompt } };
        var completion = await router.CompleteAsync(
            "selection_hint",
            messages,
            new LlmCallMetadata
            {
                BookId = bookId,
                SectionIndex = currentSection,
                UsedFutureContent = usedFutureContent,
            },
            ct);

        var sources = evidence
            .Select(e => new HintSource
            {
                SectionIndex = e.SectionIndex,
                LocationStart = e.LocationStart,
                LocationEnd = e.LocationEnd,
            })
            .ToList();

        return SelectionHintResult.ForHint(completion.Content.Trim(), sources);
    }

    private string RenderPrompt(
        string selectedText,
        int currentSection,
        int currentLocation,
        IReadOnlyList<EvidenceChunk> evidence)
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

        return settings.GetString("prompts.selectionHint")
            .Replace("{selectedText}", selectedText, StringComparison.Ordinal)
            .Replace("{currentSection}", currentSection.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{currentLocation}", currentLocation.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{context}", context.ToString(), StringComparison.Ordinal);
    }

    private static bool IsVisible(BookEntity entity, int currentSection, int currentLocation)
    {
        if (entity.FirstSeenSection is not int section)
        {
            return false;
        }

        return section < currentSection
            || (section == currentSection && (entity.FirstSeenLocation ?? 0) <= currentLocation);
    }

    private static bool Matches(BookEntity entity, string needle)
    {
        if (string.Equals(Normalize(entity.CanonicalName), needle, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return entity.Aliases.Any(alias =>
            string.Equals(Normalize(alias), needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string text) => EntityNameNormalizer.Normalize(text);
}

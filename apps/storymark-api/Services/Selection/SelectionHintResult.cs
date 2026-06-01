namespace Storymark.Api.Services.Selection;

internal enum SelectionHintKind
{
    Entity,
    Hint,
}

/// <summary>A spoiler-safe entity card (docs/plan.md §5.4). For S4.3 this is a
/// projection of the <c>book_entities</c> row; S6.1 will back it with a
/// position-scoped cached summary behind the same shape.</summary>
internal sealed record EntityCard
{
    public required string CanonicalName { get; init; }

    public required string EntityType { get; init; }

    public required IReadOnlyList<string> Aliases { get; init; }

    public required int? FirstSeenSection { get; init; }
}

internal sealed record HintSource
{
    public required int SectionIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }
}

/// <summary>The tiered result of a selection (docs/plan.md §5.5, §11.7): either a
/// cached entity card (zero LLM cost) or a fast generated hint with its sources.</summary>
internal sealed record SelectionHintResult
{
    public required SelectionHintKind Kind { get; init; }

    public Guid? EntityId { get; init; }

    public EntityCard? Card { get; init; }

    public string? Hint { get; init; }

    public IReadOnlyList<HintSource> Sources { get; init; } = [];

    public static SelectionHintResult ForEntity(Guid entityId, EntityCard card) =>
        new() { Kind = SelectionHintKind.Entity, EntityId = entityId, Card = card };

    public static SelectionHintResult ForHint(string hint, IReadOnlyList<HintSource> sources) =>
        new() { Kind = SelectionHintKind.Hint, Hint = hint, Sources = sources };
}

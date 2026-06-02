namespace Storymark.Api.Services.Memory;

/// <summary>The book-so-far memory the model returns as JSON (docs/plan.md §19.5).
/// The default prompt asks for the same fields the §12.11 table persists:
/// <see cref="SummarySoFar"/>, <see cref="Timeline"/>, and
/// <see cref="OpenThreads"/>. Lists are nullable because a model may omit empty
/// arrays; they're coalesced when persisted.</summary>
internal sealed record BookMemory
{
    public string? SummarySoFar { get; init; }

    public IReadOnlyList<string>? Timeline { get; init; }

    public IReadOnlyList<string>? OpenThreads { get; init; }
}

namespace Storymark.Api.Services.Memory;

/// <summary>The book-so-far memory the model returns as JSON (docs/plan.md §19.5).
/// The prompt asks for more fields (resolved threads, key entities, …) than the
/// §12.11 table persists; only <see cref="SummarySoFar"/>/<see cref="Timeline"/>/
/// <see cref="OpenThreads"/> are read here, the rest are ignored. Lists are nullable
/// because a model may omit empty arrays; they're coalesced when persisted.</summary>
internal sealed record BookMemory
{
    public string? SummarySoFar { get; init; }

    public IReadOnlyList<string>? Timeline { get; init; }

    public IReadOnlyList<string>? OpenThreads { get; init; }
}

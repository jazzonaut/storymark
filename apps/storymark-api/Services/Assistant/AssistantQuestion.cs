namespace Storymark.Api.Services.Assistant;

/// <summary>A Q&amp;A turn as the assistant sees it: the question, the reading
/// position, and the stateless conversation context the client carries — the
/// selected passage the conversation is anchored to (if any) and the earlier
/// turns (see docs/adr/0021-stateless-assistant-conversations.md). The service
/// clamps and sanitises both before they reach the prompt.</summary>
internal sealed record AssistantQuestion
{
    public required string Question { get; init; }

    public string? SelectedText { get; init; }

    public required IReadOnlyList<AssistantHistoryTurn> History { get; init; }

    public required int CurrentSection { get; init; }

    public required int CurrentLocation { get; init; }
}

internal sealed record AssistantHistoryTurn
{
    public required string Question { get; init; }

    public required string Answer { get; init; }
}

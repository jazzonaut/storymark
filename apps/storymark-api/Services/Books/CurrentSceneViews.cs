using Storymark.Api.Domain.Books;

namespace Storymark.Api.Services.Books;

/// <summary>A compact spoiler-safe "what is happening right now?" card for a reader
/// returning after distraction. Generated from context at-or-before the supplied
/// reading position and surfaced in the Memory panel.</summary>
internal sealed record CurrentSceneView
{
    public required string Summary { get; init; }

    public required IReadOnlyList<CurrentSceneEntityView> PresentEntities { get; init; }

    public string? Place { get; init; }

    public string? ImmediateTension { get; init; }

    public required IReadOnlyList<string> RelevantThreads { get; init; }

    public string? RecentChange { get; init; }

    public required IReadOnlyList<string> SuggestedQuestions { get; init; }

    public required IReadOnlyList<QuestionSource> Sources { get; init; }
}

internal sealed record CurrentSceneEntityView
{
    public Guid? EntityId { get; init; }

    public required string Name { get; init; }

    public required string Role { get; init; }
}

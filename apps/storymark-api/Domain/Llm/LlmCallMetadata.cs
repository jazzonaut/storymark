namespace Storymark.Api.Domain.Llm;

/// <summary>Call-site context attached to an LLM call for the audit log. The
/// spoiler-safety invariant (docs/plan.md §17) hinges on call sites setting
/// <see cref="UsedFutureContent"/> honestly when they bypass the retrieval filter.</summary>
internal sealed record LlmCallMetadata
{
    public static readonly LlmCallMetadata None = new();

    public Guid? BookId { get; init; }

    public int? SectionIndex { get; init; }

    public bool UsedFutureContent { get; init; }
}

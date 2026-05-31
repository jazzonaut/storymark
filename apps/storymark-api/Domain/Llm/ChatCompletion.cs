namespace Storymark.Api.Domain.Llm;

/// <summary>The result of a routed chat call: the model's answer plus the
/// <c>provider</c>/<c>model</c> that actually served it (resolved live from
/// settings at call time).</summary>
internal sealed record ChatCompletion
{
    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required string Content { get; init; }

    public int? TokensIn { get; init; }

    public int? TokensOut { get; init; }
}

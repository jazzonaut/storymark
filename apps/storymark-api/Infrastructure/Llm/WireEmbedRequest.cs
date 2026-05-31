using System.Text.Json.Serialization;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>The <c>{model, input}</c> embedding request body shared by the Ollama
/// (<c>/api/embed</c>) and OpenAI-compatible (<c>/embeddings</c>) wire formats.</summary>
internal sealed record WireEmbedRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input")]
    public required string Input { get; init; }
}

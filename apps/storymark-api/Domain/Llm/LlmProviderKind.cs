namespace Storymark.Api.Domain.Llm;

/// <summary>Which wire protocol (and therefore which <c>IChatModelClient</c>) a
/// provider speaks. Persisted as text.</summary>
internal enum LlmProviderKind
{
    Ollama,
    OpenAiCompatible,
}

namespace Storymark.Api.Domain.Llm;

/// <summary>A configured LLM backend (ADR-0016): local Ollama or any
/// OpenAI-compatible endpoint. Models reference a provider, so the base URL and
/// key live here once and are shared by all of the provider's models.</summary>
internal sealed class LlmProvider
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public LlmProviderKind Kind { get; set; }

    public required string BaseUrl { get; set; }

    /// <summary>AES-GCM ciphertext of the API key, or null for a provider that
    /// needs none (e.g. local Ollama). Never leaves the API in clear.</summary>
    public string? ApiKeyEncrypted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

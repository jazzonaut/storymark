namespace Storymark.Api.Domain.Llm;

/// <summary>Maps <see cref="LlmProviderKind"/> to/from its wire token — the stable
/// lowercase string used in the embedding identity (<c>ollama:nomic-embed-text</c>)
/// and the admin API.</summary>
internal static class LlmProviderKindExtensions
{
    public static string ToToken(this LlmProviderKind kind) => kind switch
    {
        LlmProviderKind.Ollama => "ollama",
        LlmProviderKind.OpenAiCompatible => "openai_compatible",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown provider kind."),
    };

    /// <summary>The embedding identity stored on <c>book_chunks.embedding_model_id</c>
    /// (<c>{kind}:{modelName}</c>, e.g. <c>ollama:nomic-embed-text</c>) — one definition
    /// so the resolver and the chunk filter can't drift (ADR-0014/0016).</summary>
    public static string EmbeddingIdentity(this LlmProviderKind kind, string modelName) =>
        $"{kind.ToToken()}:{modelName}";

    public static bool TryParseToken(string? token, out LlmProviderKind kind)
    {
        switch (token)
        {
            case "ollama":
                kind = LlmProviderKind.Ollama;
                return true;
            case "openai_compatible":
                kind = LlmProviderKind.OpenAiCompatible;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}

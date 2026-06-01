namespace Storymark.Api.Domain.Llm;

/// <summary>The settings keys that map tasks (and embedding) to a model id
/// (ADR-0016). One definition so the resolver, the seeder/healer, and the
/// embedding-swap endpoint can't drift on the literal string.</summary>
internal static class LlmSettingKeys
{
    public const string EmbeddingModelId = "llm.embedding.modelId";

    public const string TaskMapPrefix = "llm.chat.taskMap.";

    public static string TaskKey(string task) => $"{TaskMapPrefix}{task}";
}

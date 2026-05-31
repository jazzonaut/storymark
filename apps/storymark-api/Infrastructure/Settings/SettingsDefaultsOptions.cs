namespace Storymark.Api.Infrastructure.Settings;

/// <summary>Non-prompt seed defaults, bound from the <c>SettingsDefaults</c>
/// configuration section. Prompt defaults live in Prompts/PromptDefaults.cs.</summary>
internal sealed class SettingsDefaultsOptions
{
    public const string SectionName = "SettingsDefaults";

    public int UploadMaxBytes { get; init; } = 209715200;

    public bool RecapCardOnSectionEntry { get; init; } = true;

    public LlmSettingsDefaults Llm { get; init; } = new();
}

internal sealed class LlmSettingsDefaults
{
    public string EmbeddingModelId { get; init; } = "ollama:nomic-embed-text";

    public Dictionary<string, string> ChatTaskMap { get; init; } = new(StringComparer.Ordinal);
}

using Storymark.Api.Infrastructure.Llm;

namespace Storymark.Api.Infrastructure.Settings;

/// <summary>Non-prompt seed defaults, bound from the <c>SettingsDefaults</c>
/// configuration section. Prompt defaults live in Prompts/PromptDefaults.cs.</summary>
internal sealed class SettingsDefaultsOptions
{
    public const string SectionName = "SettingsDefaults";

    public int UploadMaxBytes { get; init; } = 209715200;

    public bool RecapCardOnSectionEntry { get; init; } = true;

    public LlmSettingsDefaults Llm { get; init; } = new();

    public SearchSettingsDefaults Search { get; init; } = new();
}

internal sealed class SearchSettingsDefaults
{
    /// <summary>Query-expansion groups (docs/plan.md §18.2): a category name to the
    /// words that mutually expand each other. A question word matching any member
    /// adds a retrieval query built from the whole group. Seeded from config so an
    /// operator can grow the dictionary from the admin JSON editor.</summary>
    public Dictionary<string, List<string>> Synonyms { get; init; } = new(StringComparer.Ordinal);
}

internal sealed class LlmSettingsDefaults
{
    public string EmbeddingModelId { get; init; } = "ollama:nomic-embed-text";

    public Dictionary<string, string> ChatTaskMap { get; init; } = new(StringComparer.Ordinal);

    public OpenAiSettingsDefaults OpenAi { get; init; } = new();

    public Dictionary<string, LlmCostRate> CostRates { get; init; } = new(StringComparer.Ordinal);
}

internal sealed class OpenAiSettingsDefaults
{
    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;
}

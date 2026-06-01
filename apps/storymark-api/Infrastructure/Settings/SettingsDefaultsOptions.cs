namespace Storymark.Api.Infrastructure.Settings;

/// <summary>Non-prompt seed defaults, bound from the <c>SettingsDefaults</c>
/// configuration section. Prompt defaults live in Prompts/PromptDefaults.cs.
/// LLM provider/model defaults live in the registry seeder (ADR-0016).</summary>
internal sealed class SettingsDefaultsOptions
{
    public const string SectionName = "SettingsDefaults";

    public int UploadMaxBytes { get; init; } = 209715200;

    public bool RecapCardOnSectionEntry { get; init; } = true;

    public SearchSettingsDefaults Search { get; init; } = new();

    public AssistantSettingsDefaults Assistant { get; init; } = new();
}

internal sealed class AssistantSettingsDefaults
{
    /// <summary>When true, the Q&amp;A evidence set is capped at the smaller
    /// <see cref="MaxChunksLocal"/> whenever the resolved <c>qa</c> model is local
    /// (Ollama), to keep the prompt short for slow CPU inference. Hosted models keep
    /// the full <see cref="MaxChunks"/>.</summary>
    public bool TrimLocal { get; init; } = true;

    public int MaxChunks { get; init; } = 12;

    public int MaxChunksLocal { get; init; } = 6;
}

internal sealed class SearchSettingsDefaults
{
    /// <summary>Query-expansion groups (docs/plan.md §18.2): a category name to the
    /// words that mutually expand each other. A question word matching any member
    /// adds a retrieval query built from the whole group. Seeded from config so an
    /// operator can grow the dictionary from the admin JSON editor.</summary>
    public Dictionary<string, List<string>> Synonyms { get; init; } = new(StringComparer.Ordinal);
}


namespace Storymark.Api.Prompts;

/// <summary>The placeholders each <c>prompts.*</c> template must contain for its
/// render site to work. Rendering is a plain <c>string.Replace</c>, so a missing
/// placeholder is not an error there — the content silently vanishes. This contract
/// lets the failure surface where it can be loud instead: saving an edit rejects a
/// template that drops a required placeholder, and render sites fall back to the
/// built-in default when a stored template predates one.</summary>
internal static class PromptContract
{
    private static readonly Dictionary<string, string[]> Required = new(StringComparer.Ordinal)
    {
        ["prompts.qa"] =
        [
            "{currentSection}", "{currentLocation}", "{selection}", "{history}", "{question}", "{context}",
        ],
        ["prompts.selectionHint"] =
        [
            "{selectedText}", "{currentSection}", "{currentLocation}", "{context}",
        ],
        ["prompts.sectionExtraction"] = [],
        ["prompts.entitySummary"] = [],
        ["prompts.bookMemory"] = [],
    };

    public static IReadOnlyList<string> MissingPlaceholders(string key, string template) =>
        Required.TryGetValue(key, out var placeholders)
            ? placeholders.Where(p => !template.Contains(p, StringComparison.Ordinal)).ToList()
            : [];
}

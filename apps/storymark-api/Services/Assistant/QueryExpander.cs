namespace Storymark.Api.Services.Assistant;

/// <summary>Turns a reader's question into the set of retrieval queries to run
/// (docs/plan.md §18 step 3): the original question, each capitalised n-gram
/// (a stand-in for "likely entities" until Phase 6 alias matching), and one query
/// per synonym group the question triggers. Pure and static — same shape as
/// <see cref="Books.SectionChunkingService"/>.</summary>
internal static class QueryExpander
{
    private const int DefaultMaxQueries = 6;

    // Capitalised words that are sentence scaffolding, not entities. The first word
    // of any question is capitalised, so without this "What"/"Who"/… would be mistaken
    // for entity candidates.
    private static readonly HashSet<string> QuestionStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "what", "who", "why", "where", "when", "how", "which", "whose", "whom",
        "did", "does", "do", "is", "are", "was", "were", "has", "have", "had",
        "can", "could", "would", "should", "will", "tell", "explain", "describe",
        "the", "a", "an", "in", "on", "at", "to", "of", "and", "but", "or", "if",
    };

    public static IReadOnlyList<string> BuildQueries(
        string question,
        IReadOnlyDictionary<string, List<string>> synonyms,
        int maxQueries = DefaultMaxQueries)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string candidate)
        {
            var value = candidate.Trim();
            if (value.Length > 0 && seen.Add(value))
            {
                ordered.Add(value);
            }
        }

        var trimmed = question.Trim();
        Add(trimmed);
        foreach (var entity in ExtractCapitalisedNGrams(trimmed))
        {
            Add(entity);
        }

        foreach (var group in ExpandSynonyms(trimmed, synonyms))
        {
            Add(group);
        }

        return ordered.Count <= maxQueries ? ordered : ordered[..maxQueries];
    }

    private static List<string> ExtractCapitalisedNGrams(string question)
    {
        var results = new List<string>();
        var run = new List<string>();

        foreach (var raw in question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = Clean(raw);
            if (token.Length > 0 && char.IsUpper(token[0]) && !QuestionStopWords.Contains(token))
            {
                run.Add(token);
                continue;
            }

            Flush(run, results);
        }

        Flush(run, results);
        return results;
    }

    private static void Flush(List<string> run, List<string> results)
    {
        if (run.Count > 0)
        {
            results.Add(string.Join(' ', run));
            run.Clear();
        }
    }

    private static List<string> ExpandSynonyms(
        string question,
        IReadOnlyDictionary<string, List<string>> synonyms)
    {
        if (synonyms.Count == 0)
        {
            return [];
        }

        var lowered = question.ToLowerInvariant();
        var words = lowered.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Clean)
            .Where(t => t.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var triggered = new List<string>();
        foreach (var group in synonyms.Values)
        {
            var hit = group.Any(member =>
            {
                var m = member.ToLowerInvariant();
                return m.Contains(' ', StringComparison.Ordinal)
                    ? lowered.Contains(m, StringComparison.Ordinal)
                    : words.Contains(m);
            });

            if (hit)
            {
                triggered.Add(string.Join(' ', group));
            }
        }

        return triggered;
    }

    private static string Clean(string token)
    {
        var start = 0;
        var end = token.Length - 1;
        while (start <= end && !char.IsLetterOrDigit(token[start]))
        {
            start++;
        }

        while (end >= start && !char.IsLetterOrDigit(token[end]))
        {
            end--;
        }

        return start > end ? string.Empty : token[start..(end + 1)];
    }
}

namespace Storymark.Api.Domain.Books;

/// <summary>Shared rule for deciding whether a model-supplied alias reads like a real
/// proper name rather than a generic role label. The section-extraction first-seen
/// backfill, the alias-based entity reconciliation pass, and the reader's highlighter
/// (highlight-entities.ts, which mirrors this) all gate on it, so highlighting, gating,
/// and merging agree on which aliases count as a name.</summary>
internal static class EntityAliasRules
{
    // Honorifics, pronouns, and generic role words: an alias built solely from these is a
    // role label, not a name, so it can't anchor first-seen or trigger a merge (keys are
    // lowercase to match EntityNameNormalizer.MatchKey output).
    private static readonly HashSet<string> GenericAliasWords = new(StringComparer.Ordinal)
    {
        "i", "you", "he", "she", "it", "we", "they", "him", "her", "them", "his", "hers",
        "its", "their", "theirs", "the", "a", "an", "this", "that", "these", "those",
        "mr", "mrs", "ms", "miss", "mister", "mistress", "sir", "madam", "madame", "lady",
        "lord", "dr", "doctor", "prof", "professor", "reverend", "saint", "st",
        "captain", "commander", "general", "major", "colonel", "sergeant", "lieutenant",
        "admiral", "king", "queen", "prince", "princess", "duke", "duchess", "count",
        "countess", "baron", "baroness", "father", "mother", "brother", "sister", "son",
        "daughter", "uncle", "aunt", "cousin", "master", "boy", "girl", "man", "woman",
        "child", "kid", "lad", "lass", "guard", "soldier", "servant", "maid", "stranger",
        "figure", "old", "young",
    };

    /// <summary>An alias is strong enough to anchor first-seen or trigger a merge only if it
    /// reads like a proper name: it must contain an uppercase letter (generic role nouns like
    /// "doctor" are lowercase in prose) and not be built solely from honorifics, pronouns, or
    /// role words ("Captain", "The Doctor", "Mother"). Keeps real aliases ("Frodo", "Captain
    /// Maren") while rejecting generics that would otherwise reveal or fold an unrelated
    /// entity.</summary>
    public static bool IsStrongAlias(string alias)
    {
        if (!EntityNameNormalizer.Fold(alias).Any(char.IsUpper))
        {
            return false;
        }

        // MatchKey already collapses periods and whitespace runs to single spaces and
        // lowercases, so a plain space split yields the words to test against the generic list.
        var words = EntityNameNormalizer.MatchKey(alias).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.Any(w => !GenericAliasWords.Contains(w));
    }
}

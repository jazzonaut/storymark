namespace Storymark.Api.Domain.Books;

/// <summary>Normalises an entity name or a user selection for matching against
/// <see cref="BookEntity.CanonicalName"/>/<see cref="BookEntity.Aliases"/>: trims,
/// strips surrounding punctuation, and collapses internal whitespace. Case is
/// preserved — callers compare case-insensitively (or lowercase for a dictionary
/// key). Shared by the selection-hint short-circuit and extraction's entity
/// reconciliation so both normalise identically.</summary>
internal static class EntityNameNormalizer
{
    public static string Normalize(string text)
    {
        var trimmed = text.Trim();
        var start = 0;
        var end = trimmed.Length - 1;
        while (start <= end && !char.IsLetterOrDigit(trimmed[start]))
        {
            start++;
        }

        while (end >= start && !char.IsLetterOrDigit(trimmed[end]))
        {
            end--;
        }

        if (start > end)
        {
            return string.Empty;
        }

        var core = trimmed[start..(end + 1)];
        return string.Join(' ', core.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

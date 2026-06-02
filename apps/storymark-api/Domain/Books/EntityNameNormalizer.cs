using System.Text;

namespace Storymark.Api.Domain.Books;

/// <summary>Normalises and identity-keys an entity name or a user selection for matching
/// against <see cref="BookEntity.CanonicalName"/>/<see cref="BookEntity.Aliases"/>.
/// <see cref="Normalize"/> produces the display form — folds typographic quotes/dashes to
/// ASCII, trims, strips surrounding punctuation, collapses internal whitespace, preserves
/// case. <see cref="MatchKey"/> produces the lowercase, separator-insensitive identity key
/// that the entity reconciler, the selection-hint short-circuit, the reader's highlighter
/// (highlight-entities.ts) and the first-seen backfill all key on, so every one of them
/// agrees on which spans are the same entity.</summary>
internal static class EntityNameNormalizer
{
    public static string Normalize(string text)
    {
        var trimmed = Fold(text).Trim();
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

    /// <summary>A separator-insensitive comparison key: <see cref="Normalize"/> with any
    /// run of whitespace and abbreviation periods collapsed to one space, lowercased — so a
    /// selection of "Mrs.Bancroft" resolves to an entity stored as "Mrs. Bancroft". Mirrors
    /// the reader's highlight matcher (highlight-entities.ts), which keys spans the same way,
    /// so the highlight and "Who/What is this?" lookups agree. Used only for matching; the
    /// stored canonical name keeps its original punctuation.</summary>
    public static string MatchKey(string text)
    {
        var sb = new StringBuilder(text.Length);
        var pendingSeparator = false;
        foreach (var c in Fold(text))
        {
            if (c == '.' || char.IsWhiteSpace(c))
            {
                pendingSeparator = true;
                continue;
            }

            if (pendingSeparator && sb.Length > 0)
            {
                sb.Append(' ');
            }

            pendingSeparator = false;
            sb.Append(char.ToLowerInvariant(c));
        }

        return Normalize(sb.ToString());
    }

    /// <summary>Folds typographic quotes and dashes to their ASCII forms, one char per
    /// char, so a name and a book's prose match regardless of which the source used. The
    /// reader's highlight matcher (highlight-entities.ts) folds the same set, so an entity
    /// highlights, gates, and resolves on selection at exactly the same spans.</summary>
    public static string Fold(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                '‘' or '’' or '‛' or '`' or '´' or 'ʼ' => '\'',
                '‐' or '‑' or '‒' or '–' or '—' or '―' or '−' => '-',
                _ => c,
            });
        }

        return sb.ToString();
    }
}

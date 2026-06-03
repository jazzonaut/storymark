namespace Storymark.Api.Infrastructure.Text;

/// <summary>Bounded-length text for prompts and previews: cut at a character
/// budget, never inside a surrogate pair, with a trailing ellipsis marking the
/// cut.</summary>
internal static class Ellipsis
{
    public static string Truncate(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        // Cutting between a high and low surrogate would leave an unpaired
        // half-character at the end of the truncated text.
        var cut = char.IsHighSurrogate(text[maxChars - 1]) ? maxChars - 1 : maxChars;
        return text[..cut].TrimEnd() + "…";
    }
}

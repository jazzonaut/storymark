namespace Storymark.Api.Services.Books;

/// <summary>Canonicalises EPUB-internal file paths (always <c>/</c>-separated,
/// relative to the package root) so that an image referenced from a chapter's
/// <c>&lt;img src&gt;</c> resolves to the same key the image was stored under.
/// Pure string manipulation — no IO.</summary>
internal static class EpubPath
{
    /// <summary>Normalises an absolute-in-archive path: forward slashes, no leading
    /// slash, <c>.</c>/<c>..</c> segments collapsed.</summary>
    public static string Normalize(string path)
    {
        var combined = path.Replace('\\', '/');
        return Collapse(combined.Split('/')) ?? string.Empty;
    }

    /// <summary>Resolves <paramref name="href"/> (as written in a section's markup)
    /// against the directory of <paramref name="sectionFilePath"/>. Returns the
    /// canonical archive path, or <c>null</c> if the href is an external/absolute
    /// URL or escapes the archive root.</summary>
    public static string? Resolve(string sectionFilePath, string href)
    {
        var trimmed = href.Trim();
        var cut = trimmed.AsSpan().IndexOfAny('#', '?');
        if (cut >= 0)
        {
            trimmed = trimmed[..cut];
        }

        if (trimmed.Length == 0 || IsAbsoluteUrl(trimmed) || trimmed.StartsWith('/'))
        {
            return null;
        }

        var baseDir = sectionFilePath.Replace('\\', '/');
        var lastSlash = baseDir.LastIndexOf('/');
        baseDir = lastSlash >= 0 ? baseDir[..lastSlash] : string.Empty;

        var segments = (baseDir.Length == 0 ? trimmed : baseDir + "/" + trimmed).Split('/');
        return Collapse(segments);
    }

    /// <summary>True when <paramref name="value"/> is a protocol-relative
    /// (<c>//host</c>) or scheme-qualified (<c>http:</c>, <c>mailto:</c>,
    /// <c>data:</c>, …) URL — i.e. not a path relative to the current document.
    /// Shared with the sanitiser so the drop/keep rule lives in one place.</summary>
    public static bool IsAbsoluteUrl(string value)
    {
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        var colon = value.IndexOf(':', StringComparison.Ordinal);
        var slash = value.IndexOf('/', StringComparison.Ordinal);
        return colon > 0 && (slash < 0 || colon < slash);
    }

    /// <summary>Joins path segments, dropping <c>.</c> and applying <c>..</c>;
    /// returns <c>null</c> if a <c>..</c> would escape the root.</summary>
    private static string? Collapse(string[] segments)
    {
        var stack = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                {
                    return null;
                }

                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(segment);
        }

        return string.Join('/', stack);
    }
}

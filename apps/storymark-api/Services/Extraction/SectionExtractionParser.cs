using System.Text.Json;

namespace Storymark.Api.Services.Extraction;

/// <summary>Recovers the <see cref="SectionExtraction"/> JSON object from a raw model
/// completion. Small local models wrap the object in markdown fences or prose, so we
/// slice from the first <c>{</c> to its matching <c>}</c> (brace-balanced, skipping
/// strings) before deserialising. Returns <c>null</c> when no valid object is found —
/// the caller (docs/adr/0009) retries once with a stricter prompt, then skips.</summary>
internal static class SectionExtractionParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    public static SectionExtraction? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var json = ExtractObject(raw);
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SectionExtraction>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractObject(string raw)
    {
        var start = raw.IndexOf('{', StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < raw.Length; i++)
        {
            var c = raw[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return raw[start..(i + 1)];
                    }

                    break;
                default:
                    break;
            }
        }

        return null;
    }
}

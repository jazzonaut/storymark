using System.Text.Json;

namespace Storymark.Api.Services.Extraction;

/// <summary>Recovers a typed JSON object from a raw model completion. Small local
/// models wrap the object in markdown fences or prose, so we
/// slice from the first <c>{</c> to its matching <c>}</c> (brace-balanced, skipping
/// strings) before deserialising case-insensitively. Returns <c>null</c> when no
/// valid object is found — callers (docs/adr/0009) retry once with a stricter prompt,
/// then degrade. Shared by the section-extraction and book-memory passes.</summary>
internal static class LooseJsonParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    public static T? Parse<T>(string? raw)
        where T : class
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
            return JsonSerializer.Deserialize<T>(json, Options);
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

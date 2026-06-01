using System.Text;
using System.Text.Json;

namespace Storymark.Api.Services.Extraction;

/// <summary>Recovers a typed JSON object from a raw model completion. Small local
/// models wrap the object in markdown fences or prose, so we
/// slice from the first <c>{</c> to its matching <c>}</c> (brace-balanced, skipping
/// strings) before deserialising case-insensitively. Bare Python literals
/// (<c>None</c>/<c>True</c>/<c>False</c>) the small models often emit are normalised to
/// their JSON forms first (outside strings only). Returns <c>null</c> when no valid
/// object is found — callers (docs/adr/0009) retry once with a stricter prompt, then
/// degrade. Shared by the section-extraction and book-memory passes.</summary>
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
            return JsonSerializer.Deserialize<T>(NormalizePythonLiterals(json), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Map Python literals to JSON ones, but only outside string contents (the
    // brace scanner already proved the string structure is well-formed). Without
    // this, a single `"x": None` from a small model fails the whole deserialise.
    private static string NormalizePythonLiterals(string json)
    {
        var sb = new StringBuilder(json.Length);
        var inString = false;
        var escaped = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (inString)
            {
                sb.Append(c);
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

            if (c == '"')
            {
                inString = true;
                sb.Append(c);
                continue;
            }

            if (char.IsLetter(c))
            {
                var start = i;
                while (i < json.Length && (char.IsLetterOrDigit(json[i]) || json[i] == '_'))
                {
                    i++;
                }

                var word = json[start..i];
                sb.Append(word switch
                {
                    "None" => "null",
                    "True" => "true",
                    "False" => "false",
                    _ => word,
                });
                i--;
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
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

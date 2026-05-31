using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Storymark.Api.Services.Books;

/// <summary>Converts raw EPUB section XHTML into safe, renderable HTML plus a
/// plain-text rendering with paragraph offsets (docs/plan.md §15.2 steps 7–8).
///
/// Default-deny: only the explicit tag/attribute allow-lists survive, so the
/// output can contain no <c>&lt;script&gt;</c>, no <c>on*</c> handlers, no inline
/// <c>style</c>, and no external (<c>http(s)://</c>) references — the S2.1 spoiler-
/// independent security invariants. Inline <c>&lt;img&gt;</c> sources are resolved
/// against the section's path and rewritten to the book's resource endpoint;
/// anything external or unresolved is dropped.</summary>
internal sealed class ContentSanitizationService
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.Ordinal)
    {
        "p", "div", "span", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote",
        "em", "strong", "i", "b", "u", "s", "sub", "sup", "br", "hr",
        "ul", "ol", "li", "dl", "dt", "dd", "a", "img",
        "table", "thead", "tbody", "tfoot", "tr", "td", "th",
        "pre", "code", "small", "cite", "q", "figure", "figcaption", "section", "article",
    };

    // Elements whose entire subtree is discarded (their text content is not prose).
    private static readonly HashSet<string> DropTags = new(StringComparer.Ordinal)
    {
        "script", "style", "head", "title", "svg", "math", "object", "iframe", "embed", "link", "meta",
    };

    private static readonly HashSet<string> GlobalAttributes = new(StringComparer.Ordinal)
    {
        "class", "id", "lang", "dir",
    };

    private static readonly HashSet<string> ParagraphBlocks = new(StringComparer.Ordinal)
    {
        "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "li", "figcaption", "pre", "dd", "dt",
    };

    private static readonly string ParagraphBlockSelector = string.Join(',', ParagraphBlocks);

    private readonly HtmlParser _parser = new();

    public SanitizedSection Sanitize(
        string rawXhtml,
        string sectionFilePath,
        IReadOnlyDictionary<string, ParsedImage> images,
        Guid bookId)
    {
        var document = _parser.ParseDocument(rawXhtml);
        var body = document.Body;
        var used = new HashSet<string>(StringComparer.Ordinal);
        if (body is null)
        {
            return Empty(used);
        }

        var context = new SanitizeContext(bookId, sectionFilePath, images, used);
        SanitizeChildren(body, context);

        var (plainText, paragraphs, wordCount) = ExtractParagraphs(body);
        return new SanitizedSection
        {
            SanitizedHtml = body.InnerHtml,
            PlainText = plainText,
            WordCount = wordCount,
            Paragraphs = paragraphs,
            UsedImagePaths = used,
        };
    }

    private void SanitizeChildren(INode parent, SanitizeContext context)
    {
        foreach (var child in parent.ChildNodes.ToArray())
        {
            switch (child)
            {
                case IElement element:
                    SanitizeElement(element, context);
                    break;
                case IText:
                    break;
                default:
                    // Comments, processing instructions, CDATA, doctype: drop.
                    child.RemoveFromParent();
                    break;
            }
        }
    }

    private void SanitizeElement(IElement element, SanitizeContext context)
    {
        var tag = element.LocalName;
        if (DropTags.Contains(tag))
        {
            element.RemoveFromParent();
            return;
        }

        SanitizeChildren(element, context);

        if (!AllowedTags.Contains(tag))
        {
            Unwrap(element);
            return;
        }

        if (string.Equals(tag, "img", StringComparison.Ordinal))
        {
            if (!TryRewriteImage(element, context))
            {
                element.RemoveFromParent();
            }

            return;
        }

        SanitizeAttributes(element);
    }

    private static void SanitizeAttributes(IElement element)
    {
        var tag = element.LocalName;
        foreach (var attribute in element.Attributes.ToArray())
        {
            var name = attribute.Name.ToLowerInvariant();
            if (!IsAttributeAllowed(tag, name))
            {
                element.RemoveAttribute(attribute.Name);
                continue;
            }

            if (string.Equals(tag, "a", StringComparison.Ordinal)
                && string.Equals(name, "href", StringComparison.Ordinal)
                && !IsInternalHref(attribute.Value))
            {
                element.RemoveAttribute(attribute.Name);
            }
        }
    }

    private static bool IsAttributeAllowed(string tag, string name)
    {
        if (GlobalAttributes.Contains(name))
        {
            return true;
        }

        return (tag, name) switch
        {
            ("a", "href") => true,
            ("img", "alt") or ("img", "width") or ("img", "height") => true,
            ("td", "colspan") or ("td", "rowspan") or ("th", "colspan") or ("th", "rowspan") => true,
            _ => false,
        };
    }

    private static bool IsInternalHref(string href)
    {
        var value = href.Trim();
        if (value.Length == 0)
        {
            return false;
        }

        // A fragment is always in-document; otherwise anything that isn't an
        // external/scheme URL is a relative (internal) reference.
        return value.StartsWith('#') || !EpubPath.IsAbsoluteUrl(value);
    }

    private static bool TryRewriteImage(IElement element, SanitizeContext context)
    {
        var src = element.GetAttribute("src");
        if (string.IsNullOrWhiteSpace(src))
        {
            return false;
        }

        var resolved = EpubPath.Resolve(context.SectionFilePath, src);
        if (resolved is null || !context.Images.ContainsKey(resolved))
        {
            return false;
        }

        context.UsedImages.Add(resolved);
        SanitizeAttributes(element);

        var encoded = string.Join('/', resolved.Split('/').Select(Uri.EscapeDataString));
        element.SetAttribute("src", $"/api/books/{context.BookId}/resources/{encoded}");
        return true;
    }

    private static void Unwrap(IElement element)
    {
        var parent = element.Parent;
        if (parent is null)
        {
            element.RemoveFromParent();
            return;
        }

        while (element.FirstChild is { } child)
        {
            parent.InsertBefore(child, element);
        }

        element.RemoveFromParent();
    }

    private static (string PlainText, IReadOnlyList<SanitizedParagraph> Paragraphs, int WordCount) ExtractParagraphs(IElement body)
    {
        var blocks = body.QuerySelectorAll(ParagraphBlockSelector)
            .Where(block => block.QuerySelector(ParagraphBlockSelector) is null)
            .ToList();

        var builder = new StringBuilder();
        var paragraphs = new List<SanitizedParagraph>();

        foreach (var block in blocks)
        {
            var text = NormalizeWhitespace(block.TextContent);
            if (text.Length == 0)
            {
                continue;
            }

            var start = builder.Length;
            builder.Append(text);
            paragraphs.Add(new SanitizedParagraph
            {
                Html = block.OuterHtml,
                PlainText = text,
                LocationStart = start,
                LocationEnd = builder.Length,
            });
            builder.Append("\n\n");
        }

        // Fall back to the whole body when no paragraph blocks were found, so a
        // section's text is never silently lost to unusual markup.
        if (paragraphs.Count == 0)
        {
            var text = NormalizeWhitespace(body.TextContent);
            if (text.Length > 0)
            {
                builder.Append(text);
                paragraphs.Add(new SanitizedParagraph
                {
                    Html = body.InnerHtml,
                    PlainText = text,
                    LocationStart = 0,
                    LocationEnd = text.Length,
                });
            }
        }

        var plainText = builder.ToString().TrimEnd('\n');
        return (plainText, paragraphs, CountWords(plainText));
    }

    private static SanitizedSection Empty(IReadOnlySet<string> used) => new()
    {
        SanitizedHtml = string.Empty,
        PlainText = string.Empty,
        WordCount = 0,
        Paragraphs = [],
        UsedImagePaths = used,
    };

    /// <summary>Collapses every run of whitespace (including newlines from the
    /// source markup) to a single space and trims the ends.</summary>
    private static string NormalizeWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static int CountWords(string text) =>
        text.Length == 0 ? 0 : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private sealed record SanitizeContext(
        Guid BookId,
        string SectionFilePath,
        IReadOnlyDictionary<string, ParsedImage> Images,
        HashSet<string> UsedImages);
}

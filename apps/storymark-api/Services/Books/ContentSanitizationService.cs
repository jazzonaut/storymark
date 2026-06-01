using System.Text;
using System.Text.RegularExpressions;
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
    // A heading longer than this is prose styled as a heading, not a section title;
    // we'd rather fall back to "Untitled" than show a truncated sentence.
    private const int MaxHeadingTitleLength = 200;

    // A title inferred from a non-semantic first block (all-caps or fully emphasised) is
    // held to a tighter length than an explicit <h*>, since the title signal is weaker.
    private const int MaxStyledTitleLength = 80;

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

    // First-block title signal for EPUBs with no <h*>: a chapter/part/number opener.
    // All-caps and fully-emphasised lines are recognised separately.
    private static readonly Regex TitleWordPattern = new(
        @"^(chapter|part|prologue|epilogue|book|section|canto|act|scene|interlude)\b|^\d{1,4}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture
            | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

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
            HeadingTitle = ExtractHeadingTitle(body),
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

    /// <summary>Extracts the first-heading title from already-sanitised section HTML,
    /// applying the same rule as <see cref="Sanitize"/>. Backfills a title for sections
    /// stored before heading-fallback existed, without re-parsing the source EPUB.</summary>
    public string? ExtractHeadingTitle(string sanitizedHtml)
    {
        var document = _parser.ParseDocument(sanitizedHtml);
        return document.Body is { } body ? ExtractHeadingTitle(body) : null;
    }

    /// <summary>The section's title for spine files the EPUB's table of contents doesn't
    /// name: a semantic <c>&lt;h1&gt;</c>–<c>&lt;h6&gt;</c> heading, or — when the markup has
    /// none (common in Calibre conversions that style the chapter title as a paragraph) —
    /// the first block when it reads like a title. Returns <c>null</c> when nothing
    /// qualifies, so the caller keeps its own fallback.</summary>
    private static string? ExtractHeadingTitle(IElement body)
    {
        var heading = body.QuerySelector("h1, h2, h3, h4, h5, h6");
        if (heading is not null)
        {
            var headingText = NormalizeWhitespace(heading.TextContent);
            return headingText.Length is > 0 and <= MaxHeadingTitleLength ? headingText : null;
        }

        var firstBlock = body.QuerySelectorAll(ParagraphBlockSelector)
            .FirstOrDefault(block => block.QuerySelector(ParagraphBlockSelector) is null
                && NormalizeWhitespace(block.TextContent).Length > 0);
        if (firstBlock is null)
        {
            return null;
        }

        var text = NormalizeWhitespace(firstBlock.TextContent);
        return LooksLikeTitle(firstBlock, text) ? text : null;
    }

    /// <summary>Whether the first block reads like a chapter title rather than opening
    /// prose: a chapter/part/number opener (at any length within the heading cap), or a
    /// short all-caps or fully-emphasised line. Conservative on purpose — an ordinary
    /// first sentence must not be mistaken for a title.</summary>
    private static bool LooksLikeTitle(IElement block, string text)
    {
        if (text.Length == 0 || text.Length > MaxHeadingTitleLength)
        {
            return false;
        }

        if (TitleWordPattern.IsMatch(text))
        {
            return true;
        }

        return text.Length <= MaxStyledTitleLength && (IsAllCaps(text) || IsFullyEmphasized(block, text));
    }

    private static bool IsAllCaps(string text)
    {
        var hasLetter = false;
        foreach (var ch in text)
        {
            if (!char.IsLetter(ch))
            {
                continue;
            }

            hasLetter = true;
            if (!char.IsUpper(ch))
            {
                return false;
            }
        }

        return hasLetter;
    }

    // A title styled as bold — e.g. <span class="bold">CHAPTER ONE</span> — rather than an
    // <h*>: an emphasis element whose text spans the block's entire text.
    private static bool IsFullyEmphasized(IElement block, string normalizedText)
    {
        foreach (var element in block.QuerySelectorAll("strong, b, [class]"))
        {
            if (IsEmphasis(element)
                && string.Equals(
                    NormalizeWhitespace(element.TextContent), normalizedText, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEmphasis(IElement element)
    {
        if (string.Equals(element.LocalName, "strong", StringComparison.Ordinal)
            || string.Equals(element.LocalName, "b", StringComparison.Ordinal))
        {
            return true;
        }

        var className = element.GetAttribute("class");
        return className is not null && className.Contains("bold", StringComparison.OrdinalIgnoreCase);
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

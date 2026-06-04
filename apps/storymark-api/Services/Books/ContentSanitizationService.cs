using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Storymark.Api.Services.Books;

/// <summary>Converts raw EPUB section XHTML into safe, renderable HTML plus a
/// plain-text rendering with paragraph offsets (docs/plan.md §15.2 steps 7–8).
///
/// Default-deny: only the explicit tag/attribute allow-lists survive, so the
/// output can contain no <c>&lt;script&gt;</c>, no <c>on*</c> handlers, no inline
/// <c>style</c>, and no external (<c>http(s)://</c>) references — the S2.1 spoiler-
/// independent security invariants. Inline <c>&lt;img&gt;</c> sources (including raster
/// images hoisted out of <c>&lt;svg&gt;</c> shells) are resolved against the section's
/// path and rewritten to the book's resource endpoint; anything external or
/// unresolved is dropped.</summary>
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
        "table", "caption", "thead", "tbody", "tfoot", "tr", "td", "th",
        "pre", "code", "small", "cite", "q", "figure", "figcaption", "section", "article",
    };

    // Elements whose entire subtree is discarded (their text content is not prose).
    // svg is handled separately: its subtree is dropped too, but any raster <image>
    // it wraps is hoisted out first (see ReplaceSvgImages).
    private static readonly HashSet<string> DropTags = new(StringComparer.Ordinal)
    {
        "script", "style", "head", "title", "math", "object", "iframe", "embed", "link", "meta",
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

    // Containers that must stay whole to keep their meaning: splitting an <ol> into
    // bare <li> paragraphs loses the numbering, and recursing into a <table> whose
    // cells hold <p>s shreds it into disconnected paragraphs. Each becomes a single
    // paragraph even though it contains paragraph blocks.
    private static readonly HashSet<string> AtomicBlocks = new(StringComparer.Ordinal)
    {
        "ul", "ol", "dl", "table",
    };

    // Tags whose edges render as a break even when the source markup has no
    // whitespace around them: every paragraph block, plus forced line breaks and
    // table structure. TextContent fuses words across these ("line one<br/>line two"
    // → "line oneline two"), and the plain text feeds embeddings and LLM evidence,
    // not just display — so text extraction inserts a space at each edge instead
    // (see ExtractText).
    private static readonly HashSet<string> TextBoundaryTags = new(
        ParagraphBlocks.Concat(["br", "hr", "td", "th", "tr", "caption"]), StringComparer.Ordinal);

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
        if (string.Equals(tag, "svg", StringComparison.Ordinal))
        {
            ReplaceSvgImages(element, context);
            return;
        }

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

    // EPUB 3 books commonly wrap full-page raster images — covers especially — in an
    // <svg><image href|xlink:href=...> shell for aspect-ratio scaling. We render no
    // vector content, but each raster image the shell references is hoisted out as a
    // plain <img> (alt from the svg's accessible name) so the page itself isn't lost.
    private static void ReplaceSvgImages(IElement svg, SanitizeContext context)
    {
        var parent = svg.Parent;
        var document = svg.Owner;
        if (parent is not null && document is not null)
        {
            var alt = svg.QuerySelector("title, desc")?.TextContent;
            foreach (var image in svg.QuerySelectorAll("image"))
            {
                // The HTML parser only namespace-adjusts the literal "xlink:href"; a
                // non-standard xlink prefix (xmlns:xl=... xl:href=...) survives as a
                // plain "<prefix>:href" attribute, so match href under any prefix.
                var href = image.Attributes
                    .FirstOrDefault(a => string.Equals(a.Name, "href", StringComparison.Ordinal)
                        || a.Name.EndsWith(":href", StringComparison.Ordinal))?.Value;
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                var img = document.CreateElement("img");
                img.SetAttribute("src", href);
                if (!string.IsNullOrWhiteSpace(alt))
                {
                    img.SetAttribute("alt", NormalizeWhitespace(alt));
                }

                if (TryRewriteImage(img, context))
                {
                    parent.InsertBefore(img, svg);
                }
            }
        }

        svg.RemoveFromParent();
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
        var builder = new StringBuilder();
        var paragraphs = new List<SanitizedParagraph>();

        EmitParagraphs(body, builder, paragraphs);

        // Fall back to the whole body when nothing was emitted, so a section's text is
        // never silently lost to unusual markup.
        if (paragraphs.Count == 0)
        {
            var text = ExtractText(body);
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

    // Walks a container's children in reading order. A block-level leaf becomes one
    // paragraph (its OuterHtml); a block that itself contains blocks is recursed into;
    // loose inline text and inline elements between or around blocks are flushed as their
    // own paragraph fragments. Without the run handling, text directly inside a mixed
    // block — e.g. the "x" and "z" in <div>x<p>y</p>z</div> — is dropped from paragraphs
    // even though it survives in the section HTML, so the reader never shows it.
    private static void EmitParagraphs(INode container, StringBuilder builder, List<SanitizedParagraph> paragraphs)
    {
        var run = new List<INode>();
        foreach (var child in container.ChildNodes)
        {
            if (child is IElement element)
            {
                var isBlock = ParagraphBlocks.Contains(element.LocalName);
                var isAtomic = AtomicBlocks.Contains(element.LocalName);
                // Atomic containers decide the branch on their own, so skip the
                // descendant query — it would walk their whole subtree for nothing.
                var hasBlockDescendant = !isAtomic && element.QuerySelector(ParagraphBlockSelector) is not null;
                if (isAtomic || (isBlock && !hasBlockDescendant))
                {
                    FlushRun(run, builder, paragraphs);
                    AppendParagraph(
                        element.OuterHtml,
                        ExtractText(element),
                        HasVisibleNonText(element),
                        builder,
                        paragraphs);
                    continue;
                }

                if (isBlock || hasBlockDescendant)
                {
                    FlushRun(run, builder, paragraphs);
                    EmitParagraphs(element, builder, paragraphs);
                    continue;
                }
            }

            run.Add(child);
        }

        FlushRun(run, builder, paragraphs);
    }

    // A run of loose inline nodes (text and inline elements) between block siblings,
    // wrapped in a paragraph so the reader renders it as its own block.
    private static void FlushRun(List<INode> run, StringBuilder builder, List<SanitizedParagraph> paragraphs)
    {
        if (run.Count == 0)
        {
            return;
        }

        var text = ExtractText(run);
        var hasVisual = run.Any(node => node is IElement element && HasVisibleNonText(element));
        if (text.Length > 0 || hasVisual)
        {
            var html = string.Concat(run.Select(node => node.ToHtml()));
            // A text-free run (an image plate, an <hr> scene break) is emitted bare:
            // wrapping it in <p> would be invalid for <hr> (the parser re-opens the
            // paragraph around it), and the reader gives every paragraph its own block.
            AppendParagraph(text.Length > 0 ? $"<p>{html}</p>" : html, text, hasVisual, builder, paragraphs);
        }

        run.Clear();
    }

    // Content that renders without any text: an image plate or an <hr> scene break.
    private const string VisualSelector = "img, hr";

    private static bool HasVisibleNonText(IElement element) =>
        element.Matches(VisualSelector) || element.QuerySelector(VisualSelector) is not null;

    // A text-free paragraph (an image plate, an <hr> scene break) gets an empty
    // half-open range [pos, pos) and no separator, so the plain-text coordinate
    // space is identical to what a text-only extraction would produce.
    private static void AppendParagraph(
        string html, string text, bool hasVisual, StringBuilder builder, List<SanitizedParagraph> paragraphs)
    {
        if (text.Length == 0 && !hasVisual)
        {
            return;
        }

        var start = builder.Length;
        builder.Append(text);
        paragraphs.Add(new SanitizedParagraph
        {
            Html = html,
            PlainText = text,
            LocationStart = start,
            LocationEnd = builder.Length,
        });
        if (text.Length > 0)
        {
            builder.Append("\n\n");
        }
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
            var headingText = ExtractText(heading);
            return headingText.Length is > 0 and <= MaxHeadingTitleLength ? headingText : null;
        }

        var firstBlock = body.QuerySelectorAll(ParagraphBlockSelector)
            .FirstOrDefault(block => block.QuerySelector(ParagraphBlockSelector) is null
                && ExtractText(block).Length > 0);
        if (firstBlock is null)
        {
            return null;
        }

        var text = ExtractText(firstBlock);
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
                && string.Equals(ExtractText(element), normalizedText, StringComparison.Ordinal))
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

    /// <summary>The element's rendered text on one line: like
    /// <c>TextContent</c> + <see cref="NormalizeWhitespace"/>, but with a space at
    /// every <see cref="TextBoundaryTags"/> edge so words don't fuse across breaks
    /// the source markup doesn't separate with whitespace.</summary>
    private static string ExtractText(IElement element) => ExtractText(element.ChildNodes);

    private static string ExtractText(IEnumerable<INode> nodes)
    {
        var builder = new StringBuilder();
        foreach (var node in nodes)
        {
            AppendNodeText(node, builder);
        }

        return NormalizeWhitespace(builder.ToString());
    }

    private static void AppendNodeText(INode node, StringBuilder builder)
    {
        switch (node)
        {
            case IText text:
                builder.Append(text.Data);
                break;
            case IElement element:
                {
                    var isBoundary = TextBoundaryTags.Contains(element.LocalName);
                    if (isBoundary)
                    {
                        builder.Append(' ');
                    }

                    foreach (var child in element.ChildNodes)
                    {
                        AppendNodeText(child, builder);
                    }

                    if (isBoundary)
                    {
                        builder.Append(' ');
                    }

                    break;
                }

            default:
                break;
        }
    }

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

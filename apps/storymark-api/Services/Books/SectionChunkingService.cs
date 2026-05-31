using Storymark.Api.Domain.Books;

namespace Storymark.Api.Services.Books;

/// <summary>Paragraph-aware chunking (docs/plan.md §15.3). Greedily merges
/// consecutive paragraphs into chunks of roughly <see cref="TargetWords"/> words
/// (never starting a new paragraph once a chunk would exceed <see cref="MaxWords"/>),
/// then begins the next chunk ~<see cref="OverlapWords"/> words back so adjacent
/// chunks share context. Chunk boundaries always fall on paragraph boundaries, so a
/// chunk's offsets line up with the section's plain-text coordinate space.</summary>
internal static class SectionChunkingService
{
    private const int TargetWords = 700; // midpoint of the 500–900 target band
    private const int MaxWords = 900;
    private const int OverlapWords = 125; // midpoint of the 100–150 overlap band

    /// <param name="paragraphs">The section's paragraphs in reading order.</param>
    public static IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<BookParagraph> paragraphs)
    {
        var drafts = new List<ChunkDraft>();
        if (paragraphs.Count == 0)
        {
            return drafts;
        }

        var wordCounts = new int[paragraphs.Count];
        for (var i = 0; i < paragraphs.Count; i++)
        {
            wordCounts[i] = CountWords(paragraphs[i].PlainText);
        }

        var start = 0;
        while (start < paragraphs.Count)
        {
            var words = 0;
            var end = start;
            while (end < paragraphs.Count)
            {
                // Always take at least one paragraph; otherwise stop before a paragraph
                // that would push the chunk past the hard cap. A lone paragraph longer
                // than the cap becomes its own (oversized) chunk — we never split mid-paragraph.
                if (end > start && words + wordCounts[end] > MaxWords)
                {
                    break;
                }

                words += wordCounts[end];
                end++;
                if (words >= TargetWords)
                {
                    break;
                }
            }

            drafts.Add(BuildDraft(paragraphs, start, end));

            if (end >= paragraphs.Count)
            {
                break;
            }

            // Re-include ~OverlapWords of trailing paragraphs in the next chunk, while
            // guaranteeing forward progress (the next chunk starts after `start`).
            var overlap = 0;
            var next = end;
            while (next > start + 1 && overlap < OverlapWords)
            {
                next--;
                overlap += wordCounts[next];
            }

            start = next;
        }

        return drafts;
    }

    private static ChunkDraft BuildDraft(IReadOnlyList<BookParagraph> paragraphs, int start, int end)
    {
        var text = string.Join("\n\n", Enumerable.Range(start, end - start).Select(i => paragraphs[i].PlainText));
        return new ChunkDraft
        {
            LocationStart = paragraphs[start].LocationStart,
            LocationEnd = paragraphs[end - 1].LocationEnd,
            Text = text,
            TokenEstimate = EstimateTokens(text),
        };
    }

    // Rough heuristic (~4 characters per token); exact tokenisation is the embedding
    // model's concern and isn't needed for batching/cost estimation here.
    private static int EstimateTokens(string text) =>
        text.Length == 0 ? 0 : (int)Math.Ceiling(text.Length / 4.0);

    private static int CountWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}

using Microsoft.EntityFrameworkCore;
using Pgvector;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Retrieval;

/// <summary>The single evidence path for every AI surface (CLAUDE.md, docs/plan.md
/// §13, §17). Given a query and a reading position it runs the unified hybrid query —
/// semantic (vector <c>&lt;=&gt;</c>) ∪ keyword (FTS) over the chunks the reader has
/// already passed — then reranks: each pass's scores are min-max normalised onto
/// <c>[0,1]</c> (the two scales aren't otherwise comparable) and summed per chunk, so
/// a chunk that surfaces in both passes is boosted. The spoiler filter lives in the
/// SQL, so no caller can accidentally retrieve future content.</summary>
internal sealed class SpoilerSafeRetrievalService(
    StorymarkDbContext db,
    ChatModelRouter router,
    ISettingsService settings)
{
    private const int PerPassLimit = 20;

    public async Task<IReadOnlyList<EvidenceChunk>> SearchAsync(
        Guid bookId,
        string queryText,
        int currentSection,
        int currentLocation,
        int k,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queryText) || k <= 0)
        {
            return [];
        }

        var modelId = settings.GetString("llm.embedding.modelId");
        var embedding = await router.EmbedAsync(queryText, new LlmCallMetadata { BookId = bookId }, ct);
        var queryVector = new Vector(embedding.ToArray());

        // docs/plan.md §13. The `allowed` CTE is the spoiler boundary: everything in
        // earlier sections, plus chunks of the current section that end at or before
        // the reading position. ts_rank_cd / to_tsvector use the explicit 'english'
        // regconfig so the GIN expression index (book_chunks_fts) applies.
        FormattableString sql = $@"
WITH allowed AS (
    SELECT id, section_index, chunk_index, location_start, location_end, text, embedding
    FROM book_chunks
    WHERE book_id = {bookId}
      AND embedding_model_id = {modelId}
      AND embedding IS NOT NULL
      AND (section_index < {currentSection}
           OR (section_index = {currentSection} AND location_end <= {currentLocation}))
),
semantic AS (
    SELECT id, section_index AS sectionindex, chunk_index AS chunkindex,
           location_start AS locationstart, location_end AS locationend, text,
           1 - (embedding <=> {queryVector}) AS score, 'semantic' AS source
    FROM allowed
    ORDER BY embedding <=> {queryVector}
    LIMIT {PerPassLimit}
),
keyword AS (
    SELECT id, section_index AS sectionindex, chunk_index AS chunkindex,
           location_start AS locationstart, location_end AS locationend, text,
           ts_rank_cd(to_tsvector('english', text), plainto_tsquery('english', {queryText})) AS score,
           'keyword' AS source
    FROM allowed
    WHERE to_tsvector('english', text) @@ plainto_tsquery('english', {queryText})
    ORDER BY score DESC
    LIMIT {PerPassLimit}
)
SELECT id, sectionindex, chunkindex, locationstart, locationend, text, score, source FROM semantic
UNION ALL
SELECT id, sectionindex, chunkindex, locationstart, locationend, text, score, source FROM keyword";

        var hits = await db.Database.SqlQuery<HybridHit>(sql).ToListAsync(ct);
        return Rerank(hits, k);
    }

    private static List<EvidenceChunk> Rerank(List<HybridHit> hits, int k)
    {
        var normalized = Normalize(hits, "semantic").Concat(Normalize(hits, "keyword"));

        return normalized
            .GroupBy(x => (x.Hit.SectionIndex, x.Hit.ChunkIndex))
            .Select(g => new EvidenceChunk
            {
                Id = g.First().Hit.Id,
                SectionIndex = g.Key.SectionIndex,
                ChunkIndex = g.Key.ChunkIndex,
                LocationStart = g.First().Hit.LocationStart,
                LocationEnd = g.First().Hit.LocationEnd,
                Text = g.First().Hit.Text,
                Score = g.Sum(x => x.Norm),
            })
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.SectionIndex)
            .ThenBy(e => e.ChunkIndex)
            .Take(k)
            .ToList();
    }

    private static List<(HybridHit Hit, double Norm)> Normalize(List<HybridHit> hits, string source)
    {
        var pass = hits.Where(h => string.Equals(h.Source, source, StringComparison.Ordinal)).ToList();
        if (pass.Count == 0)
        {
            return [];
        }

        var min = pass.Min(h => h.Score);
        var max = pass.Max(h => h.Score);
        var range = max - min;

        // A single hit (or an all-equal pass) carries no spread to rank by, so it
        // counts as fully relevant within its pass.
        return pass.Select(h => (Hit: h, Norm: range <= 0 ? 1.0 : (h.Score - min) / range)).ToList();
    }

    private sealed class HybridHit
    {
        public Guid Id { get; set; }

        public int SectionIndex { get; set; }

        public int ChunkIndex { get; set; }

        public int LocationStart { get; set; }

        public int LocationEnd { get; set; }

        public string Text { get; set; } = string.Empty;

        public double Score { get; set; }

        public string Source { get; set; } = string.Empty;
    }
}

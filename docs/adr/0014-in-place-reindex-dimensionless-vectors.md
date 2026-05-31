# 0014 — In-place reindex, dimensionless vectors, no HNSW

**Status:** accepted
**Date:** 2026-06-01

Supersedes the storage/coexistence mechanics of [ADR-0010](0010-version-tagged-embeddings.md). The *intent* of 0010 — no silent search degradation on an embedding-model swap — still holds; only the mechanism changes.

## Context

ADR-0010 specified a zero-downtime reindex: on a model swap, write new vectors while the old ones still serve search ("old + new coexist per chunk… storage roughly doubles"). Implementing S3.4 forced the question of how to store that, and two facts about *this* system reframed it:

1. **Coexistence requires multiple vectors per chunk**, which the implemented schema (plan §12.4: one `embedding` column on `book_chunks`, shipped in S3.1/S3.2) can't hold. It would need a separate `chunk_embeddings(chunk_id, model_id, …)` table, a join in the hybrid retrieval SQL, and per-chunk swap bookkeeping.
2. **A fixed `vector(768)` column + HNSW index can't store a different-dimension model** (all-minilm is 384, mxbai 1024) — and trying a genuinely different model is the *whole point* of a swap. HNSW requires a fixed dimension, so "any model" and "one HNSW index" are mutually exclusive.

Meanwhile the deployment target is a **single operator on a LAN with a small library** (thousands of chunks, not millions). At that scale an exact `<=>` scan is sub-millisecond — HNSW is premature optimization — and a reindex finishes in seconds-to-minutes while the operator watches it in the job dashboard.

## Decision

1. **`book_chunks.embedding` is dimensionless `vector`** (not `vector(768)`). Any model's output fits. Search never compares across models — the retrieval SQL filters `embedding_model_id = <active model>` — so vectors of different dimensions never meet in a `<=>` operation.
2. **No HNSW index.** Exact nearest-neighbour scan. Revisit if a library ever reaches a scale where it matters (it won't for the foreseeable single-operator use).
3. **Reindex is in-place.** A model swap re-embeds each chunk and overwrites its `embedding`/`embedding_model_id` in the same row (`EmbeddingJob`, re-enqueued per book — it already targets "chunks missing a vector or tagged with a different model id", so reindex and first-index are one code path, no separate `ReindexBookJob`).
4. **No-silent-degradation is preserved** by the confirm dialog (impact preview before the swap) + the active-model filter: a chunk not yet re-embedded carries the old model id and is simply filtered out, so a mid-reindex book returns *fewer* results converging to complete — never wrong-model noise.

## Consequences

- Embedding-model experimentation is unconstrained by dimension. Verified by swapping nomic-embed-text (768) → all-minilm (384) end-to-end.
- During a reindex a book's results are transiently **incomplete** (not stale-and-wrong). For a single operator who deliberately triggered the reindex and can see job progress, this is acceptable — and it's the one guarantee we trade away vs. 0010's coexistence.
- No storage doubling, no second table, no join in the hot retrieval path. Simpler than 0010's design.
- If this ever runs at multi-tenant / large-library scale, revisit: reintroduce per-dimension HNSW indexes and/or coexistence (back toward 0010).

## Alternatives considered

- **0010 as written (separate `chunk_embeddings` table, coexistence, vector(768)+HNSW)** — zero-downtime, but can't store different-dimension models without dropping HNSW anyway, and adds a table + join + per-chunk swap bookkeeping for a downtime property that barely matters at single-operator scale.
- **Keep vector(768)+HNSW, same-dimension swaps only** — keeps the index but makes the feature hollow (you can't try most other models).

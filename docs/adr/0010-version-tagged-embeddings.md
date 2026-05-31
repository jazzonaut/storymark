# 0010 — Version-tagged embeddings + warn-and-reindex flow

**Status:** accepted

## Context

Per [ADR-0007](0007-per-task-llm-model-routing.md), the embedding model is configured at runtime and can be swapped from the admin panel. Embeddings from different models live in different vector spaces — searching a `nomic-embed-text` query against `text-embedding-3-small` chunks returns noise, silently.

This is a footgun. The default behaviour of most "configure your embedding model" stories is "you can swap, but the consequences are on you" — which manifests as degraded search nobody notices for weeks.

## Decision

Every chunk vector carries `embedding_model_id` and `embedding_model_version` columns. Search **filters by the currently-selected `llm.embedding.modelId`** at query time — vectors written under a different model id are invisible to the active retrieval path.

When the admin changes `llm.embedding.modelId`:

1. A confirmation dialog shows impact: "N books, M chunks will be reindexed. Estimated time: …"
2. Confirming enqueues `ReindexBookJob` per book.
3. New vectors are written under the new model id; old vectors are deleted **per chunk as the new one lands**, not in a bulk delete-then-rewrite. Search continues to work (against the old model) for chunks not yet reindexed, until the swap completes.
4. The admin panel surfaces per-book reindex progress via Hangfire.

## Consequences

- No silent degradation on swap. Either everything is on the new model, or the swap is in progress and the admin sees that.
- Storage roughly doubles for the duration of the reindex (old + new vectors coexist per chunk). Acceptable.
- Experimentation has a real cost (re-embed everything). This is correct; it's the actual cost being made visible.
- A bug in `ReindexBookJob` would be very noticeable — search degrades for partially-reindexed books. Mitigated by keeping the job idempotent and per-chunk transactional.

## Alternatives considered

- **Refuse swap once any book is indexed** — safest, but kills experimentation forever.
- **Swap freely, leave stale vectors silently dead** — the failure mode we explicitly reject.
- **Multi-model live index** — keep vectors for every model the system has ever used, route search to all of them. Massive storage cost, complex query planning, doesn't actually answer "which model is better" anyway.

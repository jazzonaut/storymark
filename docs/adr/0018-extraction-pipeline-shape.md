# 0018 — Per-book sequential extraction, before embedding

**Status:** accepted
**Date:** 2026-06-01

## Context

S5.1 adds `SectionExtractionJob` to the processing pipeline. Two of its shape
choices deviate from the literal wording of `docs/plan.md`, so they're recorded
here. ADR-0009 already settled the *content* of the job (one LLM call per section
producing the combined JSON); this ADR is only about **job granularity** and
**where the job sits in the pipeline**.

`docs/plan.md` §15.2 says "SectionExtractionJob (per section)" — i.e. one Hangfire
job per section — and §15.4 lists the status order with `ExtractingPerSection`
*before* `Embedding`. Two forces complicate a literal reading:

- **Entity reconciliation races a unique index.** Reconciling extracted names does
  match-or-insert against `unique(book_id, canonical_name)`. Parallel per-section
  jobs would collide on a brand-new entity introduced in two sections at once, and
  rolling context (prior sections) needs ordering anyway.
- **`EmbeddingJob` is reused as the S3.4 reindex worker.** It re-embeds chunks
  tagged with a stale model id and finishes at `Indexed`. Anything that makes it
  enqueue extraction would re-extract on every reindex of an already-`Indexed`
  book and bounce its status.

## Decision

**One `SectionExtractionJob` per book, iterating sections in reading order**, one
`task:"extract"` call each. This still honours ADR-0009 (one call per section) but
removes the index race and gives deterministic ordering for free — mirroring
`EmbeddingJob`, which is already per-book.

**Extraction runs before embedding:** `ProcessBookJob → (ExtractingPerSection)
SectionExtractionJob → (Embedding) EmbeddingJob → Indexed`. Extraction needs only
section plain text, not vectors, so either order is correct; putting it first keeps
`EmbeddingJob` as the unchanged terminal step. A reindex re-runs only `EmbeddingJob`
and so never re-extracts. This also matches §15.4's status order.

`SectionExtractionJob` is idempotent: a re-run first clears the book's entities
(cascading to mentions and relationships), events, and summaries, then re-extracts.

## Consequences

- No advisory lock or job-continuation barrier needed for reconciliation; the
  sequential loop is the synchronisation.
- A book isn't `Indexed` (searchable in the UI's terms) until extraction *and*
  embedding finish. Extraction is the slow part on CPU, so Q&A-readiness is gated
  behind it. Acceptable: the UI treats anything pre-`Indexed` as still processing,
  so the user waits for the whole pipeline regardless of internal order. Because it
  is the slowest phase, the library badge now surfaces an honest section fraction
  (`Extracting N%`) the same way `Embedding` does — derived at read time from
  section-summary rows / total sections, no persisted counter. Skipped sections
  never write a summary, so the fraction can stop just short of 100% before the
  status flips; that is correct (the badge clears on the transition, not on reaching
  the denominator).
- A single section's transient call failure is logged and skipped (extraction is an
  enhancement layer); a missing extract model fails the book fast. A hard mid-run
  failure that propagates triggers a Hangfire retry, which redoes the cleared work
  from scratch — wasteful but correct at single-operator scale.
- The plan's "per section" wording is now understood as the granularity of the LLM
  call and prompt unit, not of the Hangfire job.
- **S5.2 follows the same shape.** `BookMemorySnapshotJob` (`BuildingMemorySnapshots`)
  is inserted between extraction and embedding — it needs only the section summaries,
  not vectors — so the chain becomes `ProcessBookJob → SectionExtractionJob →
  BookMemorySnapshotJob → EmbeddingJob → Indexed`, with embedding still terminal.

## Alternatives considered

- **One Hangfire job per section (literal plan).** Needs a Postgres advisory lock
  (or serialized continuations) to avoid the canonical-name index race and to order
  rolling context — more moving parts for the same result at single-operator scale.
- **Embed first, then extract.** Book becomes searchable sooner, but `EmbeddingJob`
  can no longer be the terminal step: it must branch on fresh-pipeline vs reindex to
  avoid re-extracting and bouncing `Indexed`. More coupling for a marginal gain the
  UI doesn't surface.

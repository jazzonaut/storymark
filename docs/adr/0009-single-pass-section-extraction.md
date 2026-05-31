# 0009 — Single-pass per-section extraction

**Status:** accepted

## Context

Section-level extraction needs to produce:

- a short summary,
- a list of important events with positions and involved entities,
- relationship deltas (who learned about whom, who fell out, etc.),
- entities newly introduced.

The naive approach is one LLM call per concern: four calls per section. On a CPU-only homelab with a 3B model, that's 4× the latency and 4× the cost for each book imported. With a 100-section novel, the difference is the user waiting 20 minutes vs 80 minutes after upload.

The risk in combining is that the model's output schema gets unwieldy and the failure mode (malformed JSON for one concern poisons all of them) is worse.

## Decision

**One LLM call per section.** The `SectionExtractionJob` prompts the model for a single JSON object containing `shortSummary`, `importantEvents[]`, `entitiesIntroduced[]`, `relationshipDeltas[]`, and `openThreads[]`. Mitigations for the schema-size risk:

- The prompt is explicit about the JSON shape and uses few-shot examples.
- Malformed JSON triggers a one-shot retry with a stricter prompt; second failure logs and continues with partial data.
- Each concern is persisted into its own normalised table (`section_summaries`, `book_events`, `book_relationships`, `book_entities`) — the model's combined output is decomposed at the seam between parsing and persistence.
- Entity reconciliation happens at write time: canonical-name match reuses the existing `book_entities` row; unrecognized `fromName/toName` in a relationship delta lazily creates an `Unknown`-typed entity rather than dropping the relationship.

## Consequences

- 4× fewer LLM calls during indexing.
- Smaller surface area for prompt drift — one prompt to tune instead of four.
- One large prompt is more brittle than four small ones; mitigated by retry + per-concern persistence.
- Switching to a worse model for `task: "extract"` may degrade everything at once. This is also a feature — it puts the speed/quality trade-off in one place.

## Alternatives considered

- **Separate jobs per concern** — cleaner prompts but 4× round-trips; not viable on CPU-only inference.
- **Heuristic events + LLM only for summary** — cheap but loses the structured-event richness; revisit if the combined prompt's quality is poor.
- **Streamed structured output (tools / JSON schema enforcement)** — useful when the provider supports it; we don't depend on it (Ollama's support is uneven), but `OpenAiCompatibleClient` can opt in per call when the provider does.

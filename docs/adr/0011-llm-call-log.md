# 0011 — LLM call log + cost meter as first-class admin surfaces

**Status:** accepted

## Context

Storymark calls LLMs from many sites: per-section extraction during indexing, memory-snapshot updates, entity-summary generation, Q&A from the reader, selection hints, embedding generation. With per-task model routing (ADR-0007) and editable prompts (ADR-0008), the surface area for "why did this prompt produce that response?" is large.

Three concerns compound:

- **Spoiler safety** — every call must be auditable for whether future content reached the model. The `used_future_content` flag is meaningless if there's nowhere to inspect it.
- **Prompt iteration** — tuning prompts blind is painful; seeing the last 20 calls for `task: "qa"` with their actual prompts and responses is the difference between a 30-minute iteration loop and a 5-minute one.
- **Cost** — hosted providers (per [ADR-0007](0007-per-task-llm-model-routing.md)) can rack up bills if a task is mis-routed. Without per-task / per-book cost visibility, you find out at the end of the month.

## Decision

Every chat and embedding call goes through `LlmCallRecorder`, which writes a row to `llm_calls`:

```text
id, task, provider, model, prompt, response,
tokens_in, tokens_out, latency_ms, cost_estimate,
book_id, section_index, used_future_content, created_at
```

The admin panel has a dedicated **LLM Calls** view (paginated, filterable by task/book/`used_future_content`) and a **Cost Meter** view (per-task and per-book aggregates over a rolling window). Both are bound to the same table.

Cost estimation reads per-million-token rates from `settings.llm.costRates.*` and multiplies. Local-Ollama calls have a zero rate.

Retention: 30 days rolling by default, configurable.

## Consequences

- Spoiler-safety audits are mechanical — filter to `used_future_content = true` and triage.
- Prompt iteration is fast; the LLM call log is the debugger.
- Cost is visible before the bill, not after.
- `llm_calls` grows quickly on a fresh book import (one row per section per task). Retention + an index on `(task, created_at)` keeps queries snappy; we'll move to TimescaleDB or a separate cold store if it becomes a problem (it won't at this scale).
- Prompts are logged in plaintext. If hosted providers handle sensitive content in the future, we'll add a hash-only mode for prompts. Out of scope for v1.

## Alternatives considered

- **OpenTelemetry traces only** — useful for latency but awkward for "show me the prompt and response side by side." We may add OTel exporters post-MVP, but the call log stays.
- **Per-call file logging** — works for debug, doesn't aggregate, doesn't power the admin UI.
- **No logging** — accepts the cost of blind iteration. Rejected.

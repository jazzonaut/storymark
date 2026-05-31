# 0007 — Per-task LLM model routing

**Status:** accepted

## Context

Storymark runs several distinct LLM workloads with different latency, quality, and cost profiles:

- **Embeddings** — bulk, sensitive to model swap (different vector spaces don't mix).
- **Entity extraction** — structured-JSON output, low creativity needed, can use a small fast model.
- **Section summary + events + relationships** — combined pass, benefits from a mid-size model.
- **Selection hint** — fast, short, latency-bound (reader is waiting). 1B model on CPU.
- **Q&A** — full retrieval + reasoning, the most quality-sensitive call.
- **Book memory** — rolling synthesis across summaries.

Hardcoding "use llama3.2:3b for everything" wastes capacity on cheap tasks and constrains hard tasks. Hardcoding one chat client (Ollama) locks us into local inference.

## Decision

Define an `IChatModelClient` interface with two adapters:

- `OllamaClient` (native HTTP)
- `OpenAiCompatibleClient` (covers OpenAI, Groq, OpenRouter, vLLM, LM Studio, Anthropic via litellm — anything that speaks the OpenAI Chat Completions / Embeddings shape)

Model selection is **per task**, configured in the `settings` table (`llm.chat.taskMap.qa`, `llm.chat.taskMap.extract`, etc.) and hot-reloaded. Each task resolves to a `provider:model` string at call time.

The cost-control safety net is the LLM call log (ADR-0011): every call is logged with task, model, tokens, latency, and estimated cost.

## Consequences

- Any task can be moved to a different provider+model from the admin panel without a deploy.
- New providers (Anthropic native, Google, …) land as new `IChatModelClient` adapters; no caller changes.
- The admin panel becomes an actually useful surface — not just settings, but operational levers.
- Cost transparency is non-optional. Hosted providers can rack up bills if mis-routed; the call log is the audit trail.
- One mistake — switching the embedding model with existing vectors in the database — is irreversible without reindexing. Mitigated by [ADR-0010](0010-version-tagged-embeddings.md).

## Alternatives considered

- **Single model setting** — wastes capacity, doesn't let us trade latency vs quality per surface.
- **Native Anthropic / Google adapters from day one** — useful but premature; the OpenAI-compatible shape covers ~95% of useful providers via shims. Native adapters land when Anthropic-specific features (prompt caching, tool use) become load-bearing.
- **Ollama only** — locks us out of hosted overflow for hard Q&A on a CPU-only homelab.

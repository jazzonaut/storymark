# 0016 — Provider/model registry for LLM routing

## Status

Accepted.

## Context

[ADR-0007](0007-per-task-llm-model-routing.md) routed each task to a `provider:model`
string held in settings (`llm.chat.taskMap.{task}`, `llm.embedding.modelId`), with a
single shared OpenAI-compatible endpoint (`llm.openai.baseUrl` / `llm.openai.apiKey`) and
a parallel cost map (`llm.costRates`) keyed by the same `provider:model` string.

That shape accumulated friction:

- **Magic strings.** Operators typed `openai_compatible:gpt-4o-mini` into a free-text
  field; a typo silently routed nowhere.
- **A parallel cost map** keyed by a string that had to match the task string exactly.
- **One shared hosted endpoint.** Chat and embeddings shared `llm.openai.*`, so you
  could not, say, run chat on OpenRouter and embeddings on OpenAI at once.
- **No reuse.** The same model used by five tasks was five independent strings.

## Decision

Introduce a normalised registry, modelling **local Ollama as a provider too** so local
and hosted are uniform:

```
provider { id, name, kind: ollama | openai_compatible, baseUrl, apiKey? (encrypted) }
   ↑
model    { id, label, providerId, modelName, inputPerMillion?, outputPerMillion? }
   ↑
task     → model id   (llm.chat.taskMap.{task}, llm.embedding.modelId hold a model id)
```

- **Providers and models are tables** (`llm_providers`, `llm_models`), not settings JSON.
  A provider's `api_key` is encrypted at rest with the existing
  `AesGcmSettingsEncryptor` and is never returned by the API (write-only, like secret
  settings). A JSON blob in the settings store could not hold a secret safely (it is
  returned in full by `GET /settings`) nor be marked secret per-field.
- **Task assignment stays in settings** (`llm.chat.taskMap.{task}`, `llm.embedding.modelId`)
  but now holds a **model id**. The admin UI resolves these to labels via the registry.
- **Cost lives on the model**, replacing `llm.costRates`. The router passes the rate
  to `LlmCallRecorder`, which no longer reads a cost-rate setting.
- **Clients receive the endpoint per call.** `IChatModelClient` exposes `Kind` and takes
  an `LlmEndpoint { baseUrl, apiKey }`, resolved from the model's provider. This is what
  lets two tasks target two different hosted providers.
- **Embedding identity** stored on chunks stays `{providerKind}:{modelName}` (e.g.
  `ollama:nomic-embed-text`), so the version-tagging in
  [ADR-0014](0014-in-place-reindex-dimensionless-vectors.md) is unchanged and existing
  vectors keep matching.

This supersedes the routing mechanism of [ADR-0007](0007-per-task-llm-model-routing.md)
(per-task routing remains; the encoding moves from a string to a registry reference).

## Consequences

- One place to define a model (provider, name, cost); tasks pick from a dropdown — no
  strings, no typos.
- Chat and embeddings can sit on different providers.
- Two new tables and a client refactor (the endpoint is now resolved per call, not from
  DI/settings). `llm.openai.*` and `llm.costRates` settings are retired; a startup
  seeder provisions a default local-Ollama provider and models and heals any task
  reference that does not point at a known model.
- Adding many endpoints/models at runtime is supported because they are rows, not
  fixed-key settings (the settings store cannot create new secret keys at runtime).

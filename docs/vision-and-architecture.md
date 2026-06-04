# Vision & Architecture

## What Storymark is

A self-hosted reading app for long, character-heavy books. The user reads the book the way they would on Kindle or Readera. The app builds a spoiler-safe memory of what's happened so far and surfaces it on demand:

- a chat answer to "why is everyone angry at Y?", using only chapters the reader has reached
- a tappable entity card on any character/place/object/concept
- a tiered selection popover ("Who/What is this?" → instant cached card, fast model hint, or full Q&A)
- a dismissible recap card at every chapter boundary
- a "Previously…" card on book reopen after >24h
- a visual timeline of events
- a visual relationship graph of entities

Everything filters to the reader's current position. No chapter you haven't reached is ever fed to the model, ever shown to you, or ever counted.

The reader is the first-class surface, not the AI. Storymark is meant to replace Kindle/Readera as the daily reading app — paginated layout, typography control, themes, bookmarks, fully offline once a book is downloaded.

## What it isn't

- Not a multi-tenant platform (household-scale accounts with per-user libraries and an admin role — ADR-0023/0024 — intended for one homelab, not a public service).
- Not a chatbot wrapped around books — the reader is primary, AI is the assistive layer.
- Not married to a specific LLM or vendor — model selection is per-task and runtime-configurable from the admin panel.

## Architecture at a glance

```text
Storymark Client (PWA → Capacitor in Phase 7)
  Vue 3 + Vite + Tailwind + PrimeVue + ECharts
  Dexie/IndexedDB:
    - sections (offline reading)
    - cached summaries, entity cards, timeline/graph snapshots
    - bookmarks, pending progress writes
        |
        | REST (cookie session)
        v
Storymark API
  ASP.NET Core + FastEndpoints
  Hangfire (Postgres-backed jobs)
  IChatModelClient (Ollama / OpenAI-compatible adapter)
        |
        |── PostgreSQL + pgvector
        |    books, sections, chunks (+ embeddings, version-tagged),
        |    entities, events, relationships,
        |    summaries, memory snapshots,
        |    progress, bookmarks, settings, llm_calls
        |
        |── Ollama (or BYO hosted LLM)
        |
        └── File volume (/data)
             original books, covers, extracted sections
```

Backend is canonical. Client is a smart offline replica that caches everything it has seen. New AI generation requires the backend; viewing already-cached artifacts does not.

## Why these choices

The "why" of every load-bearing decision lives in [`adr/`](adr/). The highlights:

- **pgvector instead of Qdrant** (ADR-0003). One database, one backup, hybrid retrieval is a single SQL query.
- **Hangfire on Postgres** (ADR-0004). Persistent jobs survive restarts; built-in dashboard; one fewer service.
- **PWA-first, Capacitor later** (ADR-0005). Faster iteration; Capacitor wraps the same codebase when iOS/native intents matter.
- **Per-task LLM routing** (ADR-0007). Heavy reasoning on one model, fast extraction on another, hosted overflow on a third — all changeable from the admin panel without a deploy.
- **Settings in Postgres with hot reload** (ADR-0008). The admin panel is the contract; restarts aren't.
- **Single-pass per-section extraction** (ADR-0009). Summary + events + relationship deltas + entities introduced in one LLM call — fewer round-trips on a CPU-only homelab.
- **Tests deferred** (ADR-0012). Manual verification through Phase 6; tests reintroduced once the surface stabilises.

## Where the limits are

- CPU-only inference is slow. Section extraction happens in the background; reader-time AI surfaces (selection hint, entity popup) use the 1B fast model and cache aggressively.
- IndexedDB has a per-origin quota that varies by browser. Whole-book download keeps the pattern predictable; eviction is manual via "Remove offline copy."
- Spoiler safety relies on positional filtering — `(sectionIndex, locationStart/locationEnd)`. Every LLM call goes through `SpoilerSafeRetrievalService` for evidence and `LlmCallRecorder` for accountability.

## Where it grows

The architecture is intentionally generous about what can be swapped without rewriting:

- Vector backend (today: pgvector) is reachable behind `SpoilerSafeRetrievalService` — a hypothetical "switch to Qdrant" doesn't ripple past that boundary.
- LLM providers live behind `IChatModelClient`. Anthropic, Google, vLLM, llama.cpp can all land as new adapters without touching prompt sites.
- The reader is format-agnostic via `IBookImportHandler` (today: EPUB only). PDF, MOBI/AZW, plain text, web articles can be added as handlers.
- Auth is built-in per-user accounts on a cookie session (ADR-0023/0024), and the API runs behind a reverse proxy that can layer Authelia/Authentik forward-auth for MFA/SSO without backend code changes. See [`deployment/04-auth-options.md`](deployment/04-auth-options.md).

# 0021 — Stateless assistant conversations, client-carried

**Status:** accepted
**Date:** 2026-06-03

## Context

The Q&A assistant was strictly one-shot: `POST /api/books/{id}/ask` took a question and a reading position, and the selection popover's "Ask…" escalation dropped the selected text entirely — the model never knew what passage the reader was asking about, and follow-up questions ("why did she do that?") had no referent.

Making the assistant conversational forces a decision about where the thread lives. Two extra constraints shape it:

- The reader API is anonymous (no per-user identity to key a conversation on).
- Spoiler-safety is per-position, not per-conversation: an answer produced at section 5 must not feed a prompt issued back at section 3, because the reader may have jumped backwards since.

## Decision

Conversations are stateless server-side; the client carries the thread.

- `AskRequest` gains optional `selectedText` (the passage the conversation is anchored to) and `history` (earlier `{question, answer}` turns). `AssistantService` clamps both (selection to 500 chars, history to the last 6 turns of 2000 chars each) and strips stale `[n]` citation markers from history answers before rendering them into the `prompts.qa` template via `{selection}`/`{history}` slots. The slots are part of the prompt contract (`PromptContract`): saving a `prompts.qa` edit without them is rejected, and a stored template that predates them falls back to the built-in default with an error log — a missing slot must never silently drop the carried context.
- The selected text also runs as an extra retrieval query through `SpoilerSafeRetrievalService`, so evidence about the passage is pulled in.
- The client gates what it carries by position: each turn (and the pinned selection) records the position it was produced at, and a follow-up includes only turns/selection at or behind the current position — the same boundary rule the panel already used to hide a single answer after a backward jump.
- Persistence is unchanged: each turn is still one `question_answers` row, and `LlmCallRecorder` captures the full rendered prompt (including the carried history), so auditability needs no conversation table.

## Consequences

- No migration, no conversation endpoints, no identity problem — the smallest slice that makes the assistant conversational.
- A conversation dies with the panel/page; there is no cross-device or post-reload thread. If a Q&A history view is wanted later, a `Conversation` entity and `ConversationId` on `question_answers` can be added without changing the wire shape (the carried history simply becomes server-derivable).
- The endpoint is public, so the carried context is attacker-controlled in principle; the server-side clamps bound the prompt regardless of client behaviour. The spoiler gate on carried context lives in the client — acceptable because the client already holds the future text it would be "leaking" to itself (the section endpoints serve any section anonymously), and the server-side retrieval cap (the actual spoiler chokepoint) is untouched.
- `usedFutureContent` keeps its existing meaning: *retrieval* returned future content. The carried selection/history never feeds the flag — a client that includes future text in either gets an answer recorded with `usedFutureContent = false`. Acceptable for the same reason as above, and `LlmCallRecorder` still captures the full rendered prompt, carried context included, so an audit can always see what the model was actually given.
- Token budget on small local models is bounded by the clamps rather than by a summarisation pass; very long threads silently lose their oldest turns.

## Alternatives considered

- **Persisted conversations (a `Conversation` table, turns linked by id).** Rejected for now: requires a migration and lifecycle endpoints, and with an anonymous API the thread would be per-book rather than per-reader, which makes "resume my conversation" semantics misleading on shared deployments. Revisit if a history view ships.
- **Re-sending only the last answer (not a turn list).** Rejected: multi-hop follow-ups ("and before that?") need more than one referent, and the clamps already bound the cost.
- **Server-side question condensation (rewrite the follow-up into a standalone question with an extra LLM call).** Rejected: doubles latency on slow local inference for marginal retrieval gain; the selection text as an extra retrieval query covers the common case.

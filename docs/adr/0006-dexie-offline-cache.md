# 0006 — Dexie/IndexedDB for offline cache, whole-book download on first open

**Status:** accepted

## Context

Storymark must work fully offline as a reader (plane, subway, homelab outage). AI features may degrade gracefully — already-generated summaries, entity cards, and timeline/graph snapshots stay viewable; new generation requires the backend.

The choice of client-side storage matters: `localStorage` is too small and synchronous; OPFS is better at large blobs but worse at structured queries; in-browser SQLite (wa-sqlite/sql.js) is powerful but heavy.

The choice of *what* to cache and *when* matters more. Lazy per-section caching saves storage but fails the "I forgot to download before the flight" case — a non-starter for a Kindle replacement.

## Decision

- **Storage**: IndexedDB via [Dexie](https://dexie.org/). Tables for cached books, sections, paragraphs, summaries, entity cards, timeline events, relationships, bookmarks, and a pending-progress queue.
- **Policy**: on first open of a book, the client downloads ALL sections plus any already-generated AI artifacts in one shot. After that, the book is fully readable offline. AI artifacts generated later sync opportunistically when online.
- **Cap**: eviction is manual (a "Remove offline copy" action per book in the library). No automatic eviction in v1; the user controls storage.

## Consequences

- Reading is reliably offline after the first open. Matches Kindle/Readera behaviour.
- A whole-book download takes seconds-to-minutes depending on book size; progress is shown.
- IndexedDB quota varies by browser; whole-book payloads keep this predictable.
- Pending progress writes queue in Dexie and flush on reconnect (LWW server-side, see [ADR-0008](0008-settings-in-db-with-hot-reload.md)'s sibling decision in `plan.md` §25.5).

## Alternatives considered

- **Lazy per-section caching** — saves storage but breaks the offline contract.
- **OPFS** — better for whole-blob downloads, worse for querying cached structured data (summaries, entity cards). Could pair with IndexedDB but adds complexity.
- **wa-sqlite / sql.js** — true SQL on the client, would enable full-text search offline. ~1-2 MB WASM payload and a second schema to maintain. Revisit post-Phase-6 if the offline experience needs it.
- **Capacitor SQLite** — ties offline support to the Capacitor build; breaks the PWA-first decision.

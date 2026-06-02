# 0020 — Reading-progress conflict policy: server receive-time wins

**Status:** accepted
**Date:** 2026-06-02

## Context

Reading position is backend-authoritative: one row per `(book, device)`, and the
canonical position is "the latest across devices" (`docs/plan.md` §12.5). "Latest"
needs a tiebreaker when two devices each hold a position. Two clocks are available:

- the client wall-clock (`clientTimestamp`, set on the device when the position is recorded), and
- the server receive time (`UpdatedAt`, set when the row is written).

They disagree whenever a device clock is skewed, or a position is recorded offline
and flushed later.

Originally the backend ordered the canonical latest by `UpdatedAt`, while the
reader's `resume()` chose between the local mirror and the server position by
comparing `clientTimestamp`. The two sides used **different keys**, so a device with
a future-skewed clock could reject the backend's true latest and resurrect a stale
position — a correctness bug, not just a UX wrinkle.

## Decision

Order by **server receive time (`UpdatedAt`) everywhere**. It is the single
cross-device ordering contract; the client never compares wall-clocks across devices.

In the reader (`use-reading-progress.ts` `resume()`):

- A **pending** (unsynced) local write is this device's most recent action and has
  not reached the server. It wins and is flushed; when it lands, the backend stamps
  `UpdatedAt = now`, making it canonical.
- A **synced** local mirror can only be at or behind the server's canonical latest,
  so the server's position wins whenever reachable, with the mirror as the offline
  fallback.

`clientTimestamp` is kept only as the **same-device** stale-flush guard in
`SaveAsync` (an out-of-order flush from one device can't overwrite that same device's
newer row), where a single clock is internally consistent.

## Consequences

- Skew-proof: a wrong device clock can no longer hijack the canonical position.
- Frontend and backend agree by construction; no second ordering field crosses the wire.
- **Accepted downside — last-flush-wins across devices.** A position recorded earlier
  in wall-clock time but flushed later still becomes canonical. So a device that read
  offline and reconnects later can clobber a position another device synced further
  ahead in the meantime. For a single reader moving between devices this is rare and
  self-correcting (the next read re-establishes the true position); we accept it
  rather than carry per-write version vectors.

## Alternatives considered

- **Client wall-clock wins (LWW by `clientTimestamp`).** Intuitive when clocks agree,
  and it avoids the last-flush-wins downside — but it reintroduces the skew bug a
  single bad clock causes, which is the failure we were fixing. Rejected.
- **Expose `UpdatedAt` to the client and compare it.** Doesn't help the only
  ambiguous case: a pending local write has no server `UpdatedAt` yet, so the
  comparison still needs a wall-clock or a "pending wins" rule. Adds a field to the
  contract for no decision power. Rejected in favour of the `synced`-flag rule above.
- **Version vectors / causal ordering per write.** Resolves the cross-device conflict
  honestly, but is disproportionate for a single-user reader with a handful of
  devices. Deferred; revisit if multi-user ever lands.

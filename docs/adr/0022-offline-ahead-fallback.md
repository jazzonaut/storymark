# 0022 — Offline fallback may serve a cache row stamped ahead of the reader

**Status:** accepted
**Date:** 2026-06-03

## Context

The position-aware reader caches (timeline, relationship graph, Story Guide, entity
summaries) keep one Dexie row per (book, section), stamped with the `upToLocation`
boundary of the last successful load. The read-through cache refused to serve a row
stamped *ahead* of the reader's current position — the spoiler gate — and rethrew
the fetch failure instead, so the surface showed "Couldn't load … just now" even
with data on the device.

That gate fired constantly in normal use: paging back to re-read, position restore
resolving to a slightly earlier top-of-page paragraph after a reflow, or the row
having been re-stamped forward on every page turn while the panel was open. Any
backward move plus an unreachable API (offline, or 403 from the off-VPN reverse
proxy) made every Memory surface error out, breaking ADR-0006's promise that
already-seen AI artifacts "stay viewable" offline.

The decisive observation: these rows are only ever written at the reader's own
position at panel-load time. A row "ahead of the current position" is therefore
behind the reader's *furthest-read* position in all but one case — content they
have already seen. The exception is jumping ahead without reading (e.g. a TOC jump),
opening the panel there, then returning: that row contains genuinely unread content.

## Decision

When the live fetch fails and the only cached row is stamped ahead of the reader's
current position, serve it anyway — labelled, never silently. The
`CacheFallback` reason carries `ahead: true` and the refresh indicator says the
copy is "from a little further ahead" instead of "the last saved copy".

The rest of the gate is unchanged: an ahead row never short-circuits a refresh,
is never painted while a refresh is in flight, and is overwritten by the next
successful fetch. Online behaviour is identical to before.

## Consequences

- Memory surfaces stay viewable offline/off-VPN after any backward move, restoring
  the ADR-0006 contract.
- A reader who jumps ahead without reading, opens a Memory panel there, and later
  views it offline from an earlier position can see content they haven't read.
  This is a deliberate, labelled relaxation of the spoiler invariant on the
  offline-failure path only — bounded by the user's own panel-open history, never
  by other readers or server pushes. Do not "fix" it by re-tightening the gate;
  the alternative below explains the accepted path to removing the caveat.
- The indicator wording (not just an icon) is now load-bearing: stale-ahead data
  must never be presented as current.

## Alternatives considered

- **Keep the hard gate, improve the message** — honest but useless offline: the
  panels stay empty in the most common offline posture (resumed slightly behind
  the stamp). Rejected.
- **Client-side truncation to the reader's exact position** — events and
  relationships already carry the fields the server gates on; entities would need
  `firstSeenLocation` and a summary generation stamp added to the view. Exact and
  spoiler-safe, including the jump-ahead case, and it could serve any earlier
  section from one furthest-boundary row per book. Rejected *for now* as
  disproportionate (backend view changes + Dexie schema v5 + per-surface filter
  logic); it is the natural upgrade if the jump-ahead caveat ever matters in
  practice.

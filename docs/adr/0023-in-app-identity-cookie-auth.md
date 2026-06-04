# 0023 — In-app identity via IdentityCore + cookie session; per-user books

**Status:** accepted — the admin-bearer-token aspect is superseded by [ADR-0024](0024-admin-via-user-role.md)
**Date:** 2026-06-03

## Context

Storymark has been single-reader by construction: every reader endpoint is
anonymous, per-reader state (`reading_progress`, `book_bookmarks`,
`question_answers`) is keyed by device id only, and the only credential in the
system is the shared admin bearer token (a single operator secret, not an
identity). `docs/plan.md` deferred multi-user accounts to Phase 7+; we are
pulling that forward deliberately: the household wants separate logins, separate
libraries, and separate reading state now.

The constraint set shaping the choice: the SPA is served same-origin by the API
(ADR-0013), the reader is offline-first with device caches (ADR-0006, ADR-0022),
the deployment is a single self-contained image behind Caddy on a VPN/LAN-gated
host, and the spoiler machinery is stateless (position is client-supplied per
request), so it needs no changes.

## Decision

- **Identity**: ASP.NET `AddIdentityCore<StorymarkUser>` with
  `UserOnlyStore<StorymarkUser, StorymarkDbContext, Guid>`. One `users` table — a
  trimmed `IdentityUser<Guid>` (email/phone/2FA columns ignored; lockout and
  security-stamp columns kept). No roles, no `SignInManager`, no
  `IdentityDbContext`. `UserManager` supplies versioned PBKDF2 hashing,
  rehash-on-login, lockout, and security stamps.
- **Session**: cookie authentication as the only ASP.NET scheme — HttpOnly,
  `SameSite=Strict`, 30-day sliding expiration, login/logout/me/change-password
  endpoints, and 401/403 responses instead of login-page redirects. Reader
  endpoints become secure-by-default (the `AllowAnonymous()` on each is removed);
  the user id rides the `NameIdentifier` claim. The admin bearer token is
  unchanged — it remains operator tooling, not a user role.
- **Provisioning**: no self-registration. The operator creates users through
  bearer-token-gated admin endpoints. The initial migration seeds one
  passwordless user (login rejects a null password hash) and adopts all existing
  per-reader rows and books; its password is set via the admin endpoint. No
  default credentials exist at any point.
- **Books are per-user**: `books` gains `owner_user_id` and `is_public`
  (default false). The library is own-plus-public; every book-scoped reader
  endpoint filters through a single visibility predicate (owner or public).
  Upload assigns ownership; delete and visibility changes are owner-only.
- **Content-hash dedup becomes per-owner**: uniqueness moves from
  `content_hash` to `(content_hash, owner_user_id)`. Two users may each hold a
  private row for identical content (sharing the on-disk directory); reusing
  one row would entangle ownership, and rejecting the upload would leak that
  someone else privately owns the book. Deletion removes the shared directory
  only once no row references the hash.
- **Device caches are per-user**: one IndexedDB database per account
  (`storymark.u.<userId>`) — content caches and reader settings included.
  Private book content cached for one account must never be readable from
  another's partition on a shared device. The service worker's URL-keyed
  caches follow the same rule: the books-list cache is removed (the per-user
  Dexie row replaces it); cover/inline-image caches remain shared because they
  are only reachable through partition-gated content.

## Consequences

- Storymark stays self-contained: no Authelia/OIDC dependency to deploy, and the
  password store upgrades cleanly to 2FA/email confirmation later if wanted.
- CSRF is handled by `SameSite=Strict` plus the same-origin-only API — no
  antiforgery token. Acceptable for a VPN-gated deployment; revisit before any
  internet exposure.
- Flipping a public book private orphans other readers' state for it: their
  progress/bookmark/answer rows are kept but unreachable (the book 404s for
  them, indistinguishable from absent) and resurface if the owner re-publishes.
  Deliberate: visibility wins over reading continuity, and deleting the rows
  instead would destroy positions on the owner's whim.
- The data-protection key ring — which encrypts and signs the auth cookie — is
  persisted unencrypted under the storage root (Program.cs), so anyone who can
  read that directory can forge any reader's session. Key confidentiality rides
  entirely on host filesystem trust, the same trust the book files already
  require. Revisit (`ProtectKeysWith*`) together with the CSRF posture before
  any internet exposure.
- `UserOnlyStore` methods for user claims/logins/tokens would touch entities we
  deliberately leave unmapped and will throw if ever called. Only features we
  don't use reach them; mapping those tables is the known fix if one ever does.
- Public books are cached once per user on a shared device — duplicate storage,
  accepted for the cross-account isolation guarantee.
- Session expiry while offline must not brick the reader: the app keeps serving
  the last-known user's cache partition and queues writes, prompting for login
  on reconnect (extends the ADR-0022 offline posture and the S2.4 write queue).
- `docs/plan.md`'s "multi-user accounts: Phase 7+" deferral is consumed by this
  ADR; the rest of Phase 7 (Capacitor etc.) is untouched.

## Alternatives considered

- **Forward-auth at the proxy (Authelia/Authentik)** — least code; rejected
  because it makes login an infrastructure dependency, can't express per-user
  book ownership without trusting a header end-to-end, and ties the PWA's
  offline behaviour to a proxy it can't see.
- **Full `AddIdentity` + `IdentityDbContext`** — six unused tables and an
  MVC-shaped `SignInManager` redirect stack we'd fight from FastEndpoints.
- **Hand-rolled `PasswordHasher<T>` only** — ~50 lines leaner, but loses
  lockout, security stamps, and the 2FA upgrade path. Not worth the delta.
- **JWT bearer tokens** — forces token storage in the SPA plus a refresh-token
  state machine that interacts badly with the offline layer; the HttpOnly
  sliding cookie does the same job with no client-side token handling.

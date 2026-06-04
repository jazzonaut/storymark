# 0024 — Admin access via a per-user `IsAdmin` flag on the cookie session

**Status:** accepted
**Date:** 2026-06-04

## Context

ADR-0023 introduced per-user identity with a cookie session for every reader
endpoint, but deliberately left the admin area on the pre-existing shared bearer
token: one operator secret in `settings.auth.bearerToken`, presented as an
`Authorization: Bearer` header by the admin SPA (token in `localStorage`) and as
an HTTP Basic password by the Hangfire dashboard.

With real accounts in place that split is the odd one out: two credential
systems, two sign-in UIs, a secret stored in `localStorage` (readable by any
script in the origin, unlike the HttpOnly cookie), audit columns that record a
token fingerprint instead of a name, and a rotation story that lives in a
generic settings PATCH. The user asked for admin to ride the user system
instead.

## Decision

- **`users.is_admin`** (default false) marks admin accounts. The migration
  promotes the seeded account (`…0001`) so an existing install's operator keeps
  access across the upgrade.
- **The session cookie carries the role**: `ReaderCookie.SignInAsync` adds a
  `storymark.is_admin` claim; `GET /api/auth/me` and the login response expose
  `isAdmin` so the SPA can gate the admin shell.
- **`/api/admin/*` is secure-by-default like the reader endpoints** (the
  group's `AllowAnonymous` is gone, so a missing session 401s via the cookie
  scheme); `AdminAuthPreProcessor` now only checks the admin claim and answers
  403 for a signed-in non-admin. The Hangfire dashboard filter checks the same
  claim on the same cookie.
- **Role changes rotate the security stamp.** The claim is written at sign-in,
  so granting/revoking admin calls `UpdateSecurityStampAsync` — the account's
  existing sessions die at their next stamp check and the new role applies at
  next sign-in. Tickets issued before this ADR lack the claim and read as
  non-admin until re-login.
- **The last admin cannot be demoted** (`POST /api/admin/users/{id}/admin`
  refuses), keeping the instance administrable.
- **Bootstrap**: a fresh database seeds one passwordless admin, but user
  management is itself admin-gated — a deadlock. `Auth:BootstrapPassword`
  (env/appsettings) sets that account's password on startup *only while it has
  none*; once set, the config value is inert.
- **The bearer token is deleted end-to-end**: `AdminToken`,
  `BearerTokenFingerprint`, `Auth:BearerTokenSeed`, the `auth.bearerToken`
  settings row (dropped by the migration), and the SPA's token storage.
  `updated_by` audit columns now record the admin's username.

## Consequences

- One credential system, one login UI. The admin secret is no longer in
  `localStorage`; CSRF posture is unchanged (`SameSite=Strict`, same-origin SPA,
  ADR-0023) and now covers admin too.
- Scripted/CI access to admin endpoints must log in first
  (`POST /api/auth/login` with an admin account, keep the cookie) instead of
  pasting a header. Deliberate: at single-operator scale a login call is cheap,
  and a dedicated machine credential can come back as its own feature if ever
  needed.
- A demoted admin's *current* ticket keeps the admin claim until its next stamp
  validation (≤30 min of ticket age). Accepted for a household deployment; the
  stamp rotation bounds it.
- The Hangfire dashboard no longer triggers a browser Basic-auth dialog — an
  unauthenticated visit answers a bare 401 page; the operator signs into the
  app first. Acceptable for an operator-only surface.
- The "dedicated token-rotation flow" open question from CONTEXT.md dissolves:
  the credential is now a per-user password with the existing set/reset flow.

## Alternatives considered

- **Keep the bearer token as a parallel admin path** — solves fresh-database
  bootstrap with zero new code, rejected: two credential systems forever, and
  the weaker one (localStorage token) stays the effective attack surface.
- **ASP.NET Identity roles** (`AddRoles`, `user_roles` tables) — three Identity
  tables and role-manager plumbing for what is one boolean at this scale;
  ADR-0023 already trimmed Identity deliberately. Revisit if a third role ever
  appears.
- **Re-check `is_admin` in the database per admin request** instead of trusting
  the claim — instant revocation, rejected as needless: admin traffic is tiny
  but the stamp rotation already bounds staleness, and per-request user loads
  re-introduce exactly the lookup the stamp-interval design avoids.
- **Reject pre-ADR tickets lacking the claim** at validation — forces everyone
  to re-login at upgrade; reading as non-admin until the next sign-in is the
  gentler equivalent.

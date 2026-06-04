# Auth Options

Storymark has built-in per-user authentication (ADR-0023, ADR-0024): username +
password sign-in issues an HttpOnly `SameSite=Strict` session cookie (30-day
sliding). Every endpoint — reader and admin alike — rides that session; there is
no API token. Admin access is a per-user role: accounts with `is_admin` reach
`/admin` and the Hangfire dashboard, everyone else gets 403.

That's the **default and the floor**. If you want SSO/MFA across your homelab,
layer it at the reverse proxy — the API doesn't need to know.

## Option A — Built-in accounts only (default)

First boot seeds one passwordless admin account (`reader`). Give it a password
by setting `STORYMARK_BOOTSTRAP_PASSWORD` in `infra/compose/.env` before first
boot — it's applied only while the account has no password, then ignored
forever (rotate via the in-app password change afterwards). Create further
accounts, set passwords, and grant/revoke admin from `/admin` → Users.

Use this when:

- you access Storymark over Tailscale, WireGuard, or another secured overlay,
- you expose it publicly behind a TLS-terminating reverse proxy — the stack is
  hardened for that (ADR-0025): `Secure` session cookies via the forwarded
  proto, per-account lockout plus a per-IP login throttle,
- it's a household deployment and SSO would be overkill.

Pros: zero extra infrastructure; per-user audit (`updated_by` records the admin's
username); lockout after 5 failed attempts; per-IP throttle on the login route.
Cons: passwords only — no MFA until you layer a proxy in front. Public exposure
**must** be TLS-only (Caddy's automatic HTTPS counts).

## Option B — Authelia / Authentik forward-auth

Run [Authelia](https://www.authelia.com/) or [Authentik](https://goauthentik.io/)
in front of your reverse proxy. Configure forward-auth so all requests to
Storymark hit the auth provider first; only authenticated users reach the API.

The in-app login is still required — the forward-auth provider doesn't replace
it, it adds an SSO/MFA layer on top. You log into Authelia once per session and
then into Storymark; both cookies carry through.

Example (Caddy + caddy-docker-proxy + Authelia):

```env
STORYMARK_PROXY_NETWORK=caddy
STORYMARK_REVERSE_PROXY_LABELS={"caddy":"storymark.example.com","caddy.forward_auth":"authelia:9091 { uri /api/verify?rd=https://auth.example.com }","caddy.reverse_proxy":"{{upstreams 8080}}"}
```

Use this when:

- you want MFA, or SSO across all your homelab apps,
- you already have Authelia/Authentik running.

Pros: MFA; one front door for the homelab.
Cons: extra infrastructure to operate; an auth-provider outage locks you out.

## Option C — OIDC at the API (full token-aware auth)

The API doesn't implement OIDC. The architecture is friendly to it (the cookie
scheme could gain a `JwtBearer` sibling and map IdP claims onto `users` rows),
but it's out of scope for v1. If you want this, drive it as its own ADR.

## What about Caddy's `basic_auth`?

Works as a crude extra gate, but it doesn't survive contact with a mobile PWA
gracefully — the browser prompts on every fresh session. Prefer Option B.

## API → API access (CI, scripts)

There is no standing machine credential. A script authenticates like a user:
`POST /api/auth/login` with a dedicated account's credentials, keep the cookie
jar, call the API (admin endpoints need the account to have `is_admin`). Give
automation its own account so the audit trail distinguishes it from humans, and
treat its password as a service credential. If real machine-to-machine traffic
ever materialises, a scoped API-token feature should come back as its own
decision rather than resurrecting the shared bearer token (see ADR-0024).

## What about CSRF?

The session cookie is `SameSite=Strict` and the SPA is served same-origin by the
API (ADR-0013), so cross-site requests never carry the cookie — that combination
is the CSRF posture (ADR-0023), revisited and accepted for internet exposure in
ADR-0025 (no antiforgery tokens; `__Host-` prefix rejected because it would
break plain-HTTP LAN sign-in). The posture leans on TLS-only public ingress —
don't publish the API over plain HTTP.

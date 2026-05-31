# Auth Options

The Storymark API has a built-in single-bearer-token auth mode. That's the **default and the floor**: every protected endpoint requires `Authorization: Bearer <token>` matching `settings.auth.bearerToken`. The frontend stores the token in `localStorage` (PWA) or Capacitor Secure Storage (mobile build).

If you want stronger auth — SSO, MFA, per-user audit, account lockout — layer it at the reverse proxy. The API doesn't need to know.

## Option A — Bearer token only (default)

Set `STORYMARK_BEARER_TOKEN_SEED` in `infra/compose/.env` before first boot. The seed value is written into `settings.auth.bearerToken` on the first run; rotate from the admin panel afterwards.

Use this when:

- you access Storymark over Tailscale, WireGuard, or another secured overlay,
- it's a single-user homelab and SSO would be overkill,
- you're getting started and want to defer the auth question.

Pros: zero extra infrastructure.
Cons: no audit log of *who* hit the API; the token is one bearer credential, not a session.

## Option B — Authelia / Authentik forward-auth

Run [Authelia](https://www.authelia.com/) or [Authentik](https://goauthentik.io/) in front of your reverse proxy. Configure forward-auth so all requests to Storymark hit the auth provider first; only authenticated users reach the API.

The bearer token is still required by the API itself — the forward-auth provider doesn't replace it, it adds a session/SSO layer on top. In practice, the bearer token in `localStorage` is unchanged and the SSO is invisible to the app: you log into Authelia once per session, and the cookies carry through.

Example (Caddy + caddy-docker-proxy + Authelia):

```env
STORYMARK_PROXY_NETWORK=caddy
STORYMARK_REVERSE_PROXY_LABELS={"caddy":"storymark.example.com","caddy.forward_auth":"authelia:9091 { uri /api/verify?rd=https://auth.example.com }","caddy.reverse_proxy":"{{upstreams 8080}}"}
```

Use this when:

- you have multiple users and want SSO + MFA across all your homelab apps,
- you want a real audit trail of who accessed what,
- you already have Authelia/Authentik running.

Pros: SSO across the homelab; MFA; named user audit.
Cons: extra infrastructure to operate; an auth-provider outage locks you out.

## Option C — OIDC at the API (full token-aware auth)

The API doesn't currently implement OIDC token validation. The architecture is friendly to it (an `IBearerTokenAuthFilter` already exists; an OIDC variant slots in), but it's out of scope for v1.

If you want this, the integration path is:

1. Configure your OIDC IdP (Authelia, Authentik, Keycloak, etc.) with a Storymark client.
2. Replace `BearerTokenAuthFilter` with `JwtBearer` middleware (`AddJwtBearer(opts => { opts.Authority = "..."; opts.Audience = "storymark-api"; })`).
3. Use `oidc-client-ts` in the SPA for Authorization Code + PKCE.

Open an issue if you'd like to drive this — happy to merge a PR that adds it as a second auth mode rather than a replacement.

## What about Caddy's `basic_auth`?

Works fine for a one-user homelab, but it doesn't survive contact with a mobile PWA gracefully — the browser prompts on every fresh session. Use it as a stopgap; move to Authelia or a bearer-only mode if you live on mobile.

## API → API access (CI, scripts)

CI and scripts use the bearer token directly. Treat that token as a service credential:

- Rotate from `/admin` whenever it leaks or a script is decommissioned.
- Don't share between humans and automation if you can help it — when full OIDC lands, the human path moves there and the bearer token becomes the machine-only path.

## What about CSRF?

The API enforces `Authorization: Bearer …` on every protected endpoint — there's no cookie-based session to forge. CSRF protection is therefore unnecessary; the absence of cookies is the defence.

If you switch to OIDC (Option C) and start using cookies, CSRF middleware needs to come back. Capture that in the migration ADR when it happens.

# 0025 — Internet exposure: forwarded TLS, Secure cookies, login throttle

**Status:** accepted
**Date:** 2026-06-04

## Context

ADR-0023 shipped cookie auth designed for a VPN/LAN-gated host and explicitly
flagged two checkpoints to "revisit before any internet exposure": the CSRF
posture (`SameSite=Strict` + same-origin, no antiforgery token) and the
data-protection key ring persisted unencrypted under the storage root. The
household now wants Storymark reachable without a VPN hop, which makes both
checkpoints due — plus two gaps the VPN gate had been papering over:

- Caddy terminates TLS, so the API always saw plain HTTP and
  `CookieSecurePolicy.SameAsRequest` never marked the session cookie `Secure`.
  Exposed publicly, any `http://` request an attacker can induce would carry
  the 30-day session cookie in cleartext.
- Sign-in throttling was per-account only (Identity lockout: 5 failures,
  15 minutes). An internet caller could spray attempts across accounts — or
  deliberately lock an account out — at line rate from a single source.

## Decision

- **Forwarded headers**: `UseForwardedHeaders` (`X-Forwarded-Proto`/`-For`)
  with the known-proxy allowlist cleared. Trusting any proxy is deliberate:
  production ingress is the caddy docker network only (the API publishes no
  host port), so a caller able to spoof `X-Forwarded-*` is already inside the
  trust boundary.
- **Secure cookie via forwarded proto**: `SecurePolicy` stays `SameAsRequest`;
  with the forwarded scheme applied, every session crossing TLS now gets a
  `Secure` cookie. Public ingress is TLS-only (Caddy's automatic HTTPS);
  plain-HTTP sign-in remains possible on the LAN and deliberately issues a
  non-`Secure` cookie there.
- **Per-IP login throttle**: a fixed-window rate limiter
  (10 requests/minute/source, HTTP 429) on `POST /api/auth/login`, layered on
  the per-account lockout. Lockout stops targeted guessing; the throttle stops
  cross-account spraying and slows deliberate lockout abuse.
- **CSRF posture accepted for exposure**: `SameSite=Strict` plus the
  same-origin-only SPA (ADR-0013) means cross-site requests never carry the
  cookie in any current browser. No antiforgery tokens. The `__Host-` cookie
  prefix was considered and rejected: it requires `SecurePolicy.Always`, which
  breaks plain-HTTP LAN sign-in for no gain behind TLS-only public ingress.
- **Key ring stays filesystem-persisted, unencrypted**: `ProtectKeysWith*` on
  Linux needs a certificate to protect the keys with — a secret that would
  live on the same disk, guarded by the same filesystem trust. Exposure
  changes who can reach the HTTP surface, not who can read the host disk; the
  ADR-0023 host-trust argument stands unchanged.
- **Hangfire dashboard is exposed** behind the same admin-role cookie gate as
  `/admin` (ADR-0024). Hangfire serves nothing before its authorization filter
  passes, so the pre-auth surface is one claim check.
- **`lan_vpn_only` is removed** from the shipped caddy labels. LAN/VPN gating
  and forward-auth SSO/MFA remain operator options layered at the proxy
  (`docs/deployment/04-auth-options.md`), no longer the assumed baseline.

## Consequences

- TLS on public ingress is now a hard deployment requirement, not hygiene —
  the `Secure` flag and the CSRF posture both lean on it.
- A LAN sign-in over plain HTTP gets a non-`Secure` cookie by design. Accepted
  for a household network; use the TLS hostname if that bothers you.
- The login 429 surfaces in the SPA as a generic request failure — acceptable
  for a limit only attackers should hit; revisit if a legitimate household
  device ever trips it.
- Rate-limit state is in-memory per instance — fine single-instance, would
  need rework if the API ever scales out (it has no reason to).
- Lockout-as-DoS is mitigated, not eliminated: a patient attacker inside the
  rate limit can still lock an account out. Admin password reset (which clears
  lockout) is the recovery; 2FA/forward-auth is the upgrade path if it ever
  matters.
- Anyone who can reach the API socket directly (LAN deployments publishing a
  port, the dev loop) can spoof `X-Forwarded-For` and rotate rate-limit
  buckets or pollute request logs. Accepted: production publishes no API port,
  and a LAN caller already bypasses Caddy entirely.
- ADR-0023's two "revisit before internet exposure" checkpoints are resolved
  by this ADR (CSRF: accepted as-is; key ring: host-trust argument restated
  and accepted).

## Alternatives considered

- **Stay VPN-gated** — costs nothing, but the household wants access from
  anywhere without VPN client juggling on every device; that friction was the
  whole prompt.
- **Authelia/Authentik forward-auth as a prerequisite** — adds MFA, but makes
  an external auth stack load-bearing for a household app; stays an optional
  layer (docs/deployment/04).
- **`SecurePolicy.Always` + `__Host-` prefix** — strictly stronger cookie
  attributes, but kills plain-HTTP LAN access and adds nothing while public
  ingress is TLS-only.
- **FastEndpoints' built-in `Throttle()`** — less code, but it keys on the raw
  `X-Forwarded-For` header value, which an attacker can vary per request to
  dodge the limit; the ASP.NET rate limiter partitions on the
  middleware-resolved client address instead.

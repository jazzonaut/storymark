# 0013 — SPA served by the API (same-origin, single image)

**Status:** accepted
**Date:** 2026-05-31

## Context

The Vue PWA calls the backend exclusively with relative same-origin paths
(`fetch("/api/...")`) — there is no API base URL, no `VITE_` host var, and the
service worker's `navigateFallback`/`navigateFallbackDenylist` assume the app
shell and the API share an origin. In dev this is satisfied by the Vite proxy
(`/api` → `:8088`); in production nothing served the built SPA at all. The
container image and the Compose stack carried only the API, so a deployed
`GET /` returned 404 — the frontend had never been wired into any production
serving path.

We needed a production answer to "what serves the SPA", and it had to keep the
app same-origin with the API so the relative calls and the service worker work
unchanged.

## Decision

The API image carries the built SPA and serves it. The Dockerfile gains a Node
build stage that runs `pnpm build` and copies `apps/storymark-app/dist` into the
runtime image's `wwwroot`. `Program.cs` adds `UseDefaultFiles` + `UseStaticFiles`
(with a `.webmanifest` → `application/manifest+json` mapping) and a
`MapFallback` that returns the app shell for unmatched routes — except paths
under `/api` or `/hangfire`, which still 404 so API/dashboard misses never return
HTML.

## Consequences

- One container, one image, one reverse-proxy route. The Compose stack and the
  caddy label set are unchanged — ingress already points the whole host at
  `api:8080`.
- Same-origin is structural, not configured: no CORS, and the service worker's
  shell-fallback + `/api` denylist hold exactly as authored.
- Frontend and backend ship as a single versioned artifact. For a single-operator
  homelab app this is a feature (atomic deploys, trivial rollback by image tag);
  it does mean a frontend-only change rebuilds the whole image.
- The API build now needs a Node toolchain in the build stage, lengthening a
  cold image build. BuildKit layer caching keeps incremental builds cheap.

## Alternatives considered

- **Separate nginx web container.** A second image serving `dist`, with caddy
  split-routing `/` → web and `/api` + `/hangfire` → api on one site. Cleaner
  separation, but adds a second image to build/push and makes the caddy
  two-service same-site labels fiddly — more moving parts than a single-operator
  deployment warrants.
- **Reverse proxy serves the static files directly.** caddy `file_server` for
  `/` plus `reverse_proxy /api`. Rejected: getting `dist` onto the proxy host in
  an image-based GitOps flow (CI builds images, Portainer pulls them) means a
  build artifact on the host or in a shared volume — outside the image pipeline.

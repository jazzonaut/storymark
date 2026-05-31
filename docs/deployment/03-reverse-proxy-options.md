# Reverse Proxy Options

Storymark's API exposes plain HTTP on port `8080` inside the container. There are two integration shapes, and the Compose files mirror them:

- **Standalone (base file only).** `docker-compose.yml` publishes the API on a configurable host port (`STORYMARK_API_PUBLISHED_PORT`, default `127.0.0.1:8088`). Front it with a host-level proxy pointed at that port, or reach it directly on a LAN/Tailscale. This is Options C and D below, and it's what Portainer's single-file Git stack deploys.
- **Shared Docker network (base + proxy overlay).** For label-driven proxies that watch the Docker socket (caddy-docker-proxy, Traefik), add `docker-compose.proxy.yml`. It joins the `api` service to your existing external proxy network and applies routing labels. This is Options A and B.

```bash
# Shared-network proxy:
docker network create "$STORYMARK_PROXY_NETWORK"   # once, if it doesn't exist
docker compose -f docker-compose.yml -f docker-compose.proxy.yml up -d
```

Relevant env vars:

| Variable                      | Used by            | Purpose                                                        |
| ----------------------------- | ------------------ | -------------------------------------------------------------- |
| `STORYMARK_API_PUBLISHED_PORT`| base file          | Host port to publish the API on (standalone / host-proxy).     |
| `STORYMARK_PROXY_NETWORK`     | proxy overlay      | Existing external Docker network to attach the API to.         |
| `STORYMARK_PUBLIC_HOSTS`      | proxy overlay      | Public host(s) for the routing label, no scheme.               |

> Why an overlay and not one env-switched file: Compose can't conditionally add a single network membership or merge an arbitrary set of labels from an env var. "Internal network plus an optional external one" is two files, not one substitution.

---

## Option A — caddy-docker-proxy

[caddy-docker-proxy](https://github.com/lucaslorentz/caddy-docker-proxy) watches the Docker socket for labels and generates a Caddyfile on the fly. ACME / HTTPS is automatic. This is what `docker-compose.proxy.yml` ships by default.

`.env`:

```env
STORYMARK_PROXY_NETWORK=caddy
STORYMARK_PUBLIC_HOSTS=storymark.example.com
```

```bash
docker compose -f docker-compose.yml -f docker-compose.proxy.yml up -d
```

What you get:

- `https://storymark.example.com` proxied to `api:8080`.
- ACME cert issued + renewed automatically by Caddy.
- Proxy and API talk over the `caddy` network; no published host port is required (the base file's `127.0.0.1` publish is harmless and can be dropped).

If you want auth, see [`04-auth-options.md`](04-auth-options.md) — the Authelia + Caddy pattern adds two more labels to the overlay.

---

## Option B — Traefik

[Traefik](https://traefik.io/) follows the same Docker-labels-as-config idea. Use the same overlay, but replace its `labels:` block with the Traefik set:

```yaml
# docker-compose.proxy.yml -> services.api.labels
labels:
  traefik.enable: "true"
  traefik.http.routers.storymark.rule: Host(`${STORYMARK_PUBLIC_HOSTS}`)
  traefik.http.routers.storymark.entrypoints: websecure
  traefik.http.routers.storymark.tls.certresolver: letsencrypt
  traefik.http.services.storymark.loadbalancer.server.port: "8080"
```

Adapt entrypoint, certresolver, and middleware names to your Traefik install, and set `STORYMARK_PROXY_NETWORK=traefik`.

---

## Option C — Nginx (or any host-level reverse proxy)

Run Nginx outside Compose (or in a separate stack) and point it at the API's published port. Base file only — no overlay.

In `.env`:

```env
STORYMARK_API_PUBLISHED_PORT=127.0.0.1:8088
```

In your Nginx config:

```nginx
server {
  listen 443 ssl http2;
  server_name storymark.example.com;
  # ssl_certificate / ssl_certificate_key managed elsewhere (certbot, acme.sh, ...)

  client_max_body_size 250M;   # EPUB uploads up to ~200 MB

  location / {
    proxy_pass http://127.0.0.1:8088;
    proxy_http_version 1.1;
    proxy_set_header Host              $host;
    proxy_set_header X-Real-IP         $remote_addr;
    proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_read_timeout 600;            # long LLM responses
  }
}
```

---

## Option D — No reverse proxy

Useful for LAN-only or Tailscale-only deployments where TLS isn't required and there's no shared proxy. Base file only.

```env
STORYMARK_API_PUBLISHED_PORT=8088
```

Access via `http://<host>:8088`. Tailscale users get a stable `https://<host>.<tailnet>.ts.net` URL with automatic TLS via Tailscale's MagicDNS / cert provisioning if you turn that on.

---

## Picking a default

If you have no preference, **start with caddy-docker-proxy + Authelia forward-auth** ([`04-auth-options.md`](04-auth-options.md)) using the proxy overlay. It's the lowest-friction homelab path: certificates handled, auth handled, configuration via labels colocated with the service.

In Portainer (single-file Git stacks), the overlay isn't auto-merged — see [`02-portainer-gitops.md`](02-portainer-gitops.md) for the options there.

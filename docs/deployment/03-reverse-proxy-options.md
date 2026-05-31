# Reverse Proxy Options

Storymark's API exposes plain HTTP on port `8080` inside the container. The reference Compose file publishes that on a configurable host port; everything else is up to your reverse-proxy choice.

The compose file accepts two relevant env vars:

| Variable                          | Purpose                                                              |
| --------------------------------- | -------------------------------------------------------------------- |
| `STORYMARK_PROXY_NETWORK`         | External Docker network to attach the API to. Empty = no proxy.     |
| `STORYMARK_REVERSE_PROXY_LABELS`  | JSON object of additional labels merged onto the API service.        |

This pattern lets the same compose file serve every option below without conditional rendering.

---

## Option A — caddy-docker-proxy

[caddy-docker-proxy](https://github.com/lucaslorentz/caddy-docker-proxy) watches the Docker socket for labels and generates a Caddyfile on the fly. ACME / HTTPS is automatic.

Compose env:

```env
STORYMARK_PROXY_NETWORK=caddy
STORYMARK_REVERSE_PROXY_LABELS={"caddy":"storymark.example.com","caddy.reverse_proxy":"{{upstreams 8080}}"}
```

What you get:

- `https://storymark.example.com` proxied to `api:8080`.
- ACME cert issued + renewed automatically by Caddy.
- No published host port required on the API service — proxy and API talk over the `caddy` network.

If you want auth, see [`04-auth-options.md`](04-auth-options.md) — the Authelia + Caddy pattern adds two more labels.

---

## Option B — Traefik

[Traefik](https://traefik.io/) follows the same Docker-labels-as-config idea.

```env
STORYMARK_PROXY_NETWORK=traefik
STORYMARK_REVERSE_PROXY_LABELS={"traefik.enable":"true","traefik.http.routers.storymark.rule":"Host(`storymark.example.com`)","traefik.http.routers.storymark.entrypoints":"websecure","traefik.http.routers.storymark.tls.certresolver":"letsencrypt","traefik.http.services.storymark.loadbalancer.server.port":"8080"}
```

Adapt entrypoint, certresolver, and middleware names to your Traefik install.

---

## Option C — Nginx (or any classic reverse proxy)

Run Nginx outside Compose (or in a separate stack) and point it at the API's published port.

In `.env`:

```env
STORYMARK_API_PUBLISHED_PORT=8088
STORYMARK_PROXY_NETWORK=
STORYMARK_REVERSE_PROXY_LABELS={}
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

Useful for LAN-only or Tailscale-only deployments where TLS isn't required and there's no shared proxy.

```env
STORYMARK_API_PUBLISHED_PORT=8088
STORYMARK_PROXY_NETWORK=
STORYMARK_REVERSE_PROXY_LABELS={}
```

Access via `http://<host>:8088`. Tailscale users get a stable `https://<host>.<tailnet>.ts.net` URL with automatic TLS via Tailscale's MagicDNS / cert provisioning if you turn that on.

---

## Picking a default

If you have no preference, **start with caddy-docker-proxy + Authelia forward-auth** ([`04-auth-options.md`](04-auth-options.md)). It's the lowest-friction homelab path: certificates handled, auth handled, configuration via labels colocated with the service.

The reference compose file's labels are commented examples — uncomment what you use, leave the rest.

# Deployment Prerequisites

Storymark's reference deployment is a single Linux host running Docker. Everything else — reverse proxy, auth, Git host, CI runner — is **pluggable**. This document is the minimum bar for a working install; subsequent docs cover specific configurations.

## Host

- A Linux machine (bare metal, VM, or a NAS that runs containers). The reference design assumes a modest homelab — i5-class CPU, ~24 GB RAM, CPU-only AI inference via Ollama.
- Docker Engine ≥ 24 and Docker Compose ≥ v2.
- ~50 GB free disk for books, vectors, and the Ollama model cache. More if you import large libraries.
- Outbound HTTPS, for pulling container images and (optionally) calling hosted LLM providers.

If you have a GPU and want faster local inference, swap the Ollama image for one that includes CUDA/ROCm support; the rest of the stack is unaffected.

## Networks

The compose file uses one internal network (`storymark-internal`) by default. If you front the API with a reverse proxy on a shared Docker network (e.g. `caddy`, `traefik`, `proxy`), that network is **external** and the API joins it via the `STORYMARK_PROXY_NETWORK` env var.

If you don't run a reverse proxy, just expose the published port directly (the example compose file shows both modes).

## Secrets

Two values must be set before first boot, in `infra/compose/.env`:

- `POSTGRES_PASSWORD` — Postgres password for the Storymark database.
- `STORYMARK_BOOTSTRAP_PASSWORD` — the seeded admin account's initial password; applied only while the account has none, then inert (change it in-app afterwards).
- `STORYMARK_SETTINGS_ENCRYPTION_KEY` — base64-encoded 32-byte key for encrypting secret settings at rest.

`infra/compose/.env.example` lists every supported variable with defaults and comments.

## Domains and TLS

If you expose Storymark to the public internet (or to other LAN hosts) you'll want HTTPS. The reverse-proxy options doc ([`03-reverse-proxy-options.md`](03-reverse-proxy-options.md)) covers Caddy, Traefik, Nginx, and "no proxy" modes. ACME (Let's Encrypt / ZeroSSL) is supported by all the reverse-proxy options listed.

For LAN-only or Tailscale-only deployments, HTTPS is optional but still recommended.

## Backups

`/data` (the file volume) and the Postgres database should both be backed up. See [`05-backup-and-restore.md`](05-backup-and-restore.md).

## What the deployment looks like end-to-end

1. CI builds an image on push to `master` and pushes it to your container registry.
2. CI calls the Portainer API (or another mechanism) to redeploy the stack.
3. The stack pulls the new image and restarts the API. Postgres and Ollama keep running with their volumes.
4. The reverse proxy in front routes traffic to the API, optionally enforcing auth.

You can skip steps 1–2 entirely and run `git pull && docker compose up -d` on the host — the codebase doesn't depend on CI to deploy.

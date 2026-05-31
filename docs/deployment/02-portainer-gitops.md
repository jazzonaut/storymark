# Portainer GitOps

Portainer is the reference deployment surface. You can use anything Compose-capable — `docker compose` via SSH, Argo CD, plain `systemd` units around `docker run` — but Portainer's Git-backed stacks are a pragmatic homelab default.

This document describes the Portainer-specific bits. The Compose file itself is portable.

## One-time setup

1. **Add the Git source** in Portainer → Stacks → Add stack → "Repository":
    - Repository URL: your Storymark repo URL.
    - Reference: `refs/heads/master` (or your default branch).
    - Compose path: `infra/compose/docker-compose.yml` for the standalone
      (published-port) stack. **On a caddy-docker-proxy host, point it instead
      at `infra/compose/docker-compose.caddy.yml`** — a single self-contained
      file (the overlay can't be merged in a CE Git stack, see "Reverse proxy"
      below) that joins the API to the external `caddy` network, applies the
      routing labels, and bind-mounts Postgres + book files under
      `STORYMARK_DATA_ROOT` (the Ollama model cache stays a named volume).
    - Authentication: a repo-scoped read token (CI uses this too — see `PORTAINER_REPO_USER` / `PORTAINER_REPO_PASSWORD` in [`01-ci-cd.md`](01-ci-cd.md)).
2. **Environment variables** — paste the values from `infra/compose/.env.example`, replacing placeholders. Portainer stores these against the stack and re-applies them on every redeploy.
3. **Prepare the bind-mount data dir (caddy file only).** `docker-compose.caddy.yml` bind-mounts book files at `$STORYMARK_DATA_ROOT/data`. The API runs non-root as uid `1654` (the aspnet image's `APP_UID`); a bind mount keeps the *host* directory's ownership (root by default), shadowing the image's chown, so the app can't write. Create and chown it once on the host before first deploy:

    ```bash
    sudo mkdir -p "$STORYMARK_DATA_ROOT/data"
    sudo chown -R 1654:1654 "$STORYMARK_DATA_ROOT/data"
    ```

    (Postgres fixes its own dir perms on init, so only `data/` needs this. Named-volume deploys — the base file — don't, since volumes inherit the image's ownership.)
4. **Enable webhook redeploy** (optional). Portainer exposes a webhook URL that triggers a redeploy. Wire it as the final step of your CI workflow if you don't use the `/git/redeploy` API path.
5. **First deploy**: click "Deploy the stack." Portainer pulls the repo, reads the Compose file, and brings the services up.

## Reverse proxy

Portainer CE Git stacks deploy a **single** Compose file (the "Compose path" above), so the `docker-compose.proxy.yml` overlay isn't auto-merged. Two ways to front the stack:

- **Host-level proxy (simplest).** Keep the base file; publish the API on `STORYMARK_API_PUBLISHED_PORT` and point a host-level Nginx/Caddy at it (`docs/deployment/03`, Option C/D). Nothing Portainer-specific.
- **Shared-network proxy (caddy-docker-proxy / Traefik).** Point the stack's Compose path at a file that includes the proxy network + labels. For caddy-docker-proxy this is the committed `infra/compose/docker-compose.caddy.yml`; for Traefik, copy it and swap the label block per [`03-reverse-proxy-options.md`](03-reverse-proxy-options.md) (Option B). Set `STORYMARK_PUBLIC_HOSTS` and `STORYMARK_DATA_ROOT` (plus the core secrets) as stack env vars; the `caddy` network must already exist on the host.

## Update flow

The reference flow is:

```text
PR merge → CI builds image → CI pushes image to registry
                       │
                       └─→ CI calls Portainer /api/stacks/{id}/git/redeploy
                              │
                              └─→ Portainer pulls the latest commit + image, recreates containers
```

The `image` and `deploy` jobs in [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) implement this. Pinning the image tag to the commit SHA is supported via the `STORYMARK_API_TAG` env var; useful for rollback.

## Rolling back

Portainer's stack view shows the deployed git commit. To roll back:

1. Set `STORYMARK_API_TAG` to a known-good commit SHA in the stack env.
2. Click "Update the stack" → "Redeploy."

If the rollback target also needs an older Compose file (rare; we treat the file as forward-compatible), point the stack at the previous tag/branch.

## Stopping cleanly

`docker compose down` from a shell on the host stops the stack but **preserves named volumes** (Postgres data, Ollama cache, file storage). Removing data requires `docker volume rm` or removing the bind-mount source.

## Without Portainer

Anything that calls `docker compose pull && docker compose up -d` works. Watchtower auto-pulls images by tag but doesn't handle env-var changes well; if you stick with Watchtower, treat env changes as a manual operation.

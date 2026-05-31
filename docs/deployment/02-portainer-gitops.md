# Portainer GitOps

Portainer is the reference deployment surface. You can use anything Compose-capable — `docker compose` via SSH, Argo CD, plain `systemd` units around `docker run` — but Portainer's Git-backed stacks are a pragmatic homelab default.

This document describes the Portainer-specific bits. The Compose file itself is portable.

## One-time setup

1. **Add the Git source** in Portainer → Stacks → Add stack → "Repository":
    - Repository URL: your Storymark repo URL.
    - Reference: `refs/heads/master` (or your default branch).
    - Compose path: `infra/compose/docker-compose.yml`.
    - Authentication: a repo-scoped read token (CI uses this too — see `PORTAINER_REPO_USER` / `PORTAINER_REPO_PASSWORD` in [`01-ci-cd.md`](01-ci-cd.md)).
2. **Environment variables** — paste the values from `infra/compose/.env.example`, replacing placeholders. Portainer stores these against the stack and re-applies them on every redeploy.
3. **Enable webhook redeploy** (optional). Portainer exposes a webhook URL that triggers a redeploy. Wire it as the final step of your CI workflow if you don't use the `/git/redeploy` API path.
4. **First deploy**: click "Deploy the stack." Portainer pulls the repo, reads the Compose file, and brings the services up.

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

# CI/CD

The workflow at [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) is the reference. It is designed to run on **both GitHub Actions and Forgejo Actions** (Forgejo's Actions runner is GitHub Actions–compatible at the action and expression level). Nothing in the workflow is GitHub-specific beyond the standard `actions/checkout`, `pnpm/action-setup`, `actions/setup-node`, and `actions/setup-dotnet` actions, all of which work on Forgejo Runner.

## What the workflow does

| Job        | Trigger                              | Purpose                                                       |
| ---------- | ------------------------------------ | ------------------------------------------------------------- |
| `changes`  | every push / PR                      | Detects which subtree changed; downstream jobs gate on it.    |
| `web`      | when frontend or shared paths change | `pnpm install` → `pnpm lint` → `pnpm typecheck` → `pnpm build`. |
| `dotnet`   | when backend or shared paths change  | `dotnet restore` → `dotnet format --verify-no-changes` → `dotnet build -warnaserror`. |
| `image`    | push to default branch only          | Builds a multi-stage Docker image, pushes to the configured registry under two tags: `latest` and `${commit_sha}`. On github.com it pushes to ghcr.io with zero configuration; elsewhere it skips cleanly when `REGISTRY_TOKEN` isn't set. |
| `deploy`   | push to default branch only          | Calls Portainer's `/api/stacks/{id}/git/redeploy` to repull + restart the live stack. Skips when Portainer secrets aren't set or no image was pushed. |

Tests are intentionally absent ([ADR-0012](../adr/0012-tests-deferred.md)). When tests return, `web` and `dotnet` gain test steps.

## Required secrets

The workflow reads everything via secrets so the same code can target different hosts. All are optional — set them only for the features you use.

### Image build + push

| Secret             | Default                                  | Purpose                                  |
| ------------------ | ---------------------------------------- | ---------------------------------------- |
| `REGISTRY_HOST`    | derived from `github.server_url` (`ghcr.io` when that is `github.com`) | Container registry host (`forge.example.com`). |
| `REGISTRY_USER`    | derived from `GITHUB_REPOSITORY_OWNER`   | Registry username.                       |
| `REGISTRY_TOKEN`   | the ambient `GITHUB_TOKEN` on github.com; — elsewhere | Registry password or PAT with `write:packages`. |
| `REGISTRY_IMAGE`   | `${REGISTRY_HOST}/${REGISTRY_USER}/storymark-api` | Override the full image reference. |

On **github.com** no registry secret is needed: the job grants itself `packages: write` and pushes to ghcr.io with the workflow's own `GITHUB_TOKEN`. The resulting GHCR package is **private by default** — flip it to public once in the package settings if you want others to pull it. Set `REGISTRY_TOKEN` (and friends) only to target a different registry.

On **Forgejo** (or any other host), if `REGISTRY_TOKEN` is not set the `image` job (and therefore `deploy`) skips with a notice instead of failing — the validation jobs alone keep CI green, which is the out-of-the-box state for a fresh clone. If your Git host's built-in container registry has the credentials baked in (typical for Forgejo), `REGISTRY_USER` and `REGISTRY_TOKEN` are the only required ones.

### Portainer redeploy

| Secret                       | Purpose                                                |
| ---------------------------- | ------------------------------------------------------ |
| `PORTAINER_URL`              | Base URL of the Portainer API (`https://portainer.example.com`). |
| `PORTAINER_API_KEY`          | API key for a user with stack-update permission.       |
| `PORTAINER_STACK_ID`         | Numeric stack id (from Portainer UI).                 |
| `PORTAINER_ENDPOINT_ID`      | Numeric endpoint id.                                  |
| `PORTAINER_REPO_USER`        | Repo username Portainer uses to pull the compose file. |
| `PORTAINER_REPO_PASSWORD`    | Token/password for the above.                          |

If you don't use Portainer, delete the `deploy` job and ship the image however you like (Watchtower, `docker compose pull && up -d` via SSH, Argo CD, etc.).

### Runtime environment forwarded into the stack

The deploy step calls Portainer's `/stacks/{id}/git/redeploy` endpoint and **passes through the runtime env vars** so a single source of truth (the secrets) drives every redeploy:

| Secret                              | Purpose                                                |
| ----------------------------------- | ------------------------------------------------------ |
| `STORYMARK_PUBLIC_URL`              | Public URL the SPA loads from (used by the service worker scope and any deep links). |
| `STORYMARK_PROXY_NETWORK`           | External Docker network for the reverse proxy (`caddy`, `traefik`, …). Empty = no proxy. |
| `STORYMARK_REVERSE_PROXY_LABELS`    | JSON map of labels applied to the `api` service (see [`03-reverse-proxy-options.md`](03-reverse-proxy-options.md)). |

Use whatever subset you need. The compose file has sensible defaults for any unset variable.

## Forgejo Actions specifics

Forgejo's runner is GitHub Actions-compatible, but:

- Path: workflows live under `.github/workflows/` or `.forgejo/workflows/`. The reference uses `.github/workflows/` because it works on both platforms.
- Pre-built actions are resolved from `code.forgejo.org/actions/...` or the configured marketplace. Common actions (`checkout@v4`, `setup-node@v4`, `setup-dotnet@v4`, `pnpm/action-setup@v4`) are mirrored.
- Secrets are managed in the repo Settings → Actions Secrets page, identical to GitHub.

If you're on GitHub, the workflow runs unmodified. If you're on Forgejo, ensure your runner has `node`, `pnpm`, and a Docker socket available (or use a runner image that bundles them).

## Local equivalent

Everything CI does is reproducible locally with:

```bash
task ci          # lint + typecheck + build (frontend + backend)
docker build -f apps/storymark-api/Dockerfile -t storymark-api:dev .
```

If a PR fails CI for something not reproducible locally, the workflow is buggy — open an issue.

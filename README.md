# Storymark

> Never lose the thread.

Storymark is a self-hosted, offline-first reading app for people who enjoy books but sometimes forget character names, past events, places, relationships, and plot threads. It pairs a clean **Readera-style paginated reader** with **spoiler-safe memory features** powered by a local (or BYO) LLM:

- ask free-text questions about the book ("why is everyone angry at Y?")
- select any passage to get an instant entity card or fast model hint
- see a dismissible recap card at every chapter boundary
- browse a visual **timeline of events** and **relationship graph**, both filtered to your current reading position
- never see content from chapters you haven't reached

The reader works **fully offline** once a book is downloaded; AI features degrade gracefully when the backend is unreachable.

---

## Repo layout

```text
storymark/
├── apps/
│   ├── storymark-app/      Vue 3 PWA (reader + admin panel), Capacitor later
│   └── storymark-api/      ASP.NET Core API (FastEndpoints, EF Core, Hangfire)
├── packages/
│   └── shared-types/       TypeScript contracts shared between app and tooling
├── infra/
│   └── compose/            Reference Docker Compose stack + .env.example
├── docs/                   Plan, ADRs, conventions, deployment guides
├── .github/workflows/      CI (works on GitHub Actions and Forgejo Actions)
├── CLAUDE.md               Working instructions for Claude Code in this repo
├── CONTEXT.md              Current state pointer for any AI agent
├── pnpm-workspace.yaml     Workspace + exact-pinned dependency catalog
└── Taskfile.yml            Convenience runner over pnpm + dotnet
```

---

## Quick start

Prereqs (host or devcontainer):

- Node 22 LTS + pnpm 10
- .NET SDK (latest LTS)
- Docker + Docker Compose
- [go-task](https://taskfile.dev) (optional; everything also works via the underlying tools)

```bash
pnpm install
cp infra/compose/.env.example infra/compose/.env   # edit before first up
task compose:up                                    # postgres + ollama up
task api                                           # backend with hot reload
task dev                                           # frontend dev server (separate terminal)
```

Open [http://localhost:5173](http://localhost:5173).

On first boot, the admin panel at `/admin` is gated by the bearer token in `infra/compose/.env` (`STORYMARK_BEARER_TOKEN_SEED`). Set a real value before exposing the API beyond `localhost`.

---

## Deployment

The reference deployment is a Docker Compose stack pulled and redeployed by [Portainer](https://www.portainer.io/) from a Git repo, fronted by a reverse proxy that handles TLS. Both the reverse proxy and the auth layer are **pluggable**:

- Reverse proxy options — see [`docs/deployment/03-reverse-proxy-options.md`](docs/deployment/03-reverse-proxy-options.md) for `caddy-docker-proxy`, Traefik, Nginx, or none.
- Auth options — see [`docs/deployment/04-auth-options.md`](docs/deployment/04-auth-options.md) for Authelia/Authentik forward-auth, OIDC, or the default single-bearer-token mode.

The reference compose file ([`infra/compose/docker-compose.yml`](infra/compose/docker-compose.yml)) ships with `caddy-docker-proxy` labels commented in, and a single env var (`STORYMARK_PROXY_NETWORK`) controls which external network the API joins. If you don't run any reverse proxy, just expose the published port.

CI/CD reference is in [`docs/deployment/01-ci-cd.md`](docs/deployment/01-ci-cd.md) — the workflow under `.github/workflows/ci.yml` runs on both GitHub Actions and Forgejo Actions, builds the image, pushes to a configurable registry, and (optionally) triggers a Portainer redeploy via API.

---

## Docs

- [`docs/vision-and-architecture.md`](docs/vision-and-architecture.md) — what Storymark is and the high-level architecture.
- [`docs/plan.md`](docs/plan.md) — full implementation plan (long).
- [`docs/issues.md`](docs/issues.md) — Phase 0–6 backlog as vertical-slice issues.
- [`docs/delivery-plan.md`](docs/delivery-plan.md) — phase-by-phase milestones.
- [`docs/adr/`](docs/adr/) — architecture decision records.
- [`docs/conventions/`](docs/conventions/) — code style, naming, commits, PRs.
- [`docs/deployment/`](docs/deployment/) — server prereqs, CI/CD, Portainer, reverse proxy, auth.

---

## License

MIT — see [`LICENSE`](LICENSE).

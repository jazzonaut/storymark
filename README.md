# <img src="apps/storymark-app/public/pwa-192x192.png" width="32" alt="Storymark icon"> Storymark

> A book reader that remembers with you.

Storymark is a self-hosted reading app with a memory. You read the way you would on any good e-reader: real pages, your own typography, fully offline once a book is on your device. While you read, Storymark keeps track of the story — who everyone is, what's happened, who's plotting against whom — and answers from that memory whenever you need a reminder.

The catch with every "chat with your book" idea is spoilers. Storymark's memory is **spoiler-safe by construction**: every answer, recap, timeline, and character card is built only from the chapters you've actually reached. Anything past your bookmark doesn't exist yet, on screen or in the model's context.

It runs on your own hardware, against a local LLM through Ollama or any OpenAI-compatible provider you point it at. So when you pick an epic fantasy back up after two weeks and can't remember why everyone hates the captain: just ask.

Curious what it looks like? **[Screenshots →](docs/screenshots.md)**

---

## Features

The reader comes first. Storymark is meant to replace Kindle or Readera as your daily reading app, not to be a chatbot wrapped around books.

- **Paginated reader**

  Readera-style pages (no endless scrolling), with typography controls and light, sepia, and dark themes.

- **Bookmarks and progress sync**

  Your reading position and bookmarks live on your server, so you can pick up on another device exactly where you stopped.

- **Whole-book offline**

  Download a book once and read it with no connection at all. Anything the memory has already shown you (recaps, cards, charts) is cached on the device too.

- **Installable PWA**

  One app for phone, tablet, and desktop.

The memory is what makes it different. As a book is processed, Storymark builds a structured memory of its events, characters, places, and relationships. While you read, that memory is always one tap away:

- **Ask questions in plain language**

  "Why is everyone angry at Y?" gets an answer with sources you can jump straight to.

- **"Who/What is this?" on any selection**

  Select a passage for an instant card or a fast model hint, or tap any character name in the text for a summary of what you know about them, so far.

- **The story so far**

  A rolling summary at your exact reading position.

- **Scene at a glance**

  What's happening right now, who's present, where you are, and what the immediate tension is.

- **Timeline and relationship graph**

  A chronological event timeline and a force-directed character graph, both cut off at where you are in the book.

- **Recaps that respect your pace**

  A dismissible recap card at chapter boundaries, and a "Previously…" card when you come back after a break.

Every one of these surfaces obeys the same rule: **content from chapters you haven't reached is never shown to you, never fed to the model, and never counted.**

---

## How spoiler safety works

Spoiler safety is the core product invariant, not a filter bolted on top:

- Your reading position is a precise `(section, character offset)` pair.
- All evidence for any LLM call flows through a single retrieval service that filters at the database level: a chunk, entity, event, or relationship from beyond your position simply never reaches the prompt.
- Every LLM call is recorded in an audit log with the evidence it saw and a `used_future_content` flag, so a regression in the boundary is detectable, not silent.
- Timeline, graph, entity cards, and recaps are all generated and cached *per position*, so even cached artifacts can't leak ahead.

---

## Self-hosting

Storymark ships as a single API container (which also serves the web app), plus PostgreSQL with pgvector and Ollama. A reference Docker Compose stack lives in [`infra/compose/`](infra/compose/).

```bash
git clone https://github.com/jazzonaut/storymark.git && cd storymark

# Configure the stack — set passwords/keys, point at the prebuilt image
cp infra/compose/.env.example infra/compose/.env   # edit before first up
echo "STORYMARK_API_IMAGE=ghcr.io/jazzonaut/storymark-api" >> infra/compose/.env

# Start it
docker compose -f infra/compose/docker-compose.yml --env-file infra/compose/.env up -d

# Pull the default local models
docker exec storymark-ollama ollama pull llama3.2:3b
docker exec storymark-ollama ollama pull llama3.2:1b
docker exec storymark-ollama ollama pull nomic-embed-text
```

Prefer building from source? Skip the `STORYMARK_API_IMAGE` line and run `docker compose up -d --build` from `infra/compose/` instead — the local override builds the image from the repo.

Open the published port (default `127.0.0.1:8088`), upload an EPUB, and let the background pipeline index it. Models, prompts, and per-task routing are all configurable from the admin panel at `/admin`. You can also point any task at an OpenAI-compatible hosted provider instead of (or alongside) Ollama — see [`docs/deployment/06-hosted-models.md`](docs/deployment/06-hosted-models.md) for suggested settings.

A CPU-only homelab box is a first-class target: background extraction takes its time, and reader-time AI features use a small fast model with aggressive caching.

### A note on access control

Storymark has **built-in per-user accounts**: username + password sign-in with an HttpOnly session cookie, per-user libraries and reading state, and an `is_admin` role that gates the admin panel and job dashboard. First boot seeds one admin account — give it a password via `STORYMARK_BOOTSTRAP_PASSWORD` (applied only while the account has none), then manage users from `/admin`. Public exposure behind a TLS-terminating reverse proxy is supported and hardened for (`Secure` cookies, login rate limiting, per-account lockout — see ADR-0025); for MFA/SSO put your reverse proxy's auth layer (Authelia/Authentik forward-auth) in front. See [`docs/deployment/04-auth-options.md`](docs/deployment/04-auth-options.md) and the rest of [`docs/deployment/`](docs/deployment/) for reverse-proxy options, CI/CD, Portainer GitOps, and backup guidance.

---

## Architecture at a glance

```text
Vue 3 PWA (Vite, Tailwind, ECharts, Dexie/IndexedDB offline cache)
        |  REST
        v
ASP.NET Core API (FastEndpoints) + Hangfire background jobs
        ├── PostgreSQL + pgvector  (books, sections, chunks + embeddings,
        |                           entities, events, relationships, memory
        |                           snapshots, settings, LLM call audit log)
        ├── Ollama or any OpenAI-compatible LLM provider
        └── File volume            (originals, covers, extracted sections)
```

The backend is canonical; the client is a smart offline replica that caches everything it has seen. New AI generation needs the backend, but viewing anything already cached does not.

The "why" behind every load-bearing decision is written down in [`docs/adr/`](docs/adr/). Start with [`docs/vision-and-architecture.md`](docs/vision-and-architecture.md) for the full picture.

---

## Development

Prereqs: Node 22 or newer + pnpm 10, the .NET 10 SDK (pinned in `global.json`), Docker, and optionally [go-task](https://taskfile.dev).

```bash
pnpm install
cp infra/compose/.env.example infra/compose/.env   # edit before first up
task compose:up                                    # postgres + ollama
task api                                           # backend with hot reload
task dev                                           # frontend dev server (separate terminal)
```

Open [http://localhost:5173](http://localhost:5173). Before pushing, run **`task ci`**: it runs the same lint, format, typecheck, and build gates as CI across both apps.

### Repo layout

```text
storymark/
├── apps/
│   ├── storymark-app/      Vue 3 PWA (reader + admin panel)
│   └── storymark-api/      ASP.NET Core API (FastEndpoints, EF Core, Hangfire)
├── packages/
│   └── shared-types/       TypeScript contracts shared between app and tooling
├── infra/compose/          Reference Docker Compose stack + .env.example
├── docs/                   Vision, plan, ADRs, conventions, deployment guides
└── .github/workflows/      CI (works on GitHub Actions and Forgejo Actions)
```

---

## Status

Storymark is young and honest about it:

- **Format support**: EPUB only today. The import path is pluggable; PDF and others can land as handlers.
- **Household-scale accounts**: per-user libraries and reading state with a single `is_admin` role. Passwords only — no MFA or OIDC built in (layer a reverse proxy for that).
- **Quality scales with your model**: small local models (1B–3B) run everywhere but extract sparser timelines and graphs than larger ones. Prompts and per-task model choice are tunable at runtime from the admin panel.
- **Automated tests are still thin**: the core was built against manually verified acceptance criteria ([ADR-0012](docs/adr/0012-tests-deferred.md)); a spoiler-safety harness exists (`pnpm test:spoiler`) and broader coverage is in progress.
- **PWA today, native later**: a Capacitor wrap of the same codebase is planned for iOS/Android niceties.

---

## Contributing

Bug reports, feature ideas, and PRs are all welcome. A few things to know before opening a PR:

- `task ci` runs the same gates CI does: lint, format check, typecheck, and build for both the frontend and the backend.
- Conventions live in [`docs/conventions/`](docs/conventions/): strict TypeScript, exact-pinned dependencies, Conventional Commits.
- Every load-bearing decision has an ADR in [`docs/adr/`](docs/adr/). If a change goes against one, open an issue first so we can talk it over.
- There's no full test suite yet ([ADR-0012](docs/adr/0012-tests-deferred.md)), so include a short note in your PR describing what you verified manually.

---

## License

MIT — see [`LICENSE`](LICENSE).

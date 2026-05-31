# 0008 — Settings in Postgres with hot reload

**Status:** accepted

## Context

Storymark exposes an operator-grade admin panel: LLM provider mapping, per-task model selection, prompt text, feature flags, upload limits, embedding model id, cost rates, the bearer token itself. These change frequently during prompt tuning and model experimentation. Restarting the API container on every change is friction we don't need.

The alternatives most teams reach for — env vars + `appsettings.json` — require a restart and either fight Docker (recreating the container to change one var) or fight Git (committing prompts).

## Decision

- **Storage**: a `settings` table in Postgres with `key text primary key, value jsonb, value_type text, is_secret bool, updated_at timestamptz, updated_by text`.
- **Secrets**: `is_secret = true` values are encrypted at rest with AES-GCM, key from a single env var (`Settings__EncryptionKey`).
- **Hot reload**: writers issue `NOTIFY settings_changed`. The `SettingsService` listens and invalidates its in-memory cache. Reads are fast (in-memory hit) and stay correct.
- **Seeding**: on startup, defaults from `appsettings.json` populate any missing keys. This is the only role `appsettings.json` plays for tunables.
- **Prompts**: stored under `prompts.*` keys. The defaults seeded at startup live in source as constants for diff visibility, but the DB is the runtime source of truth.

## Consequences

- Tuning prompts, swapping models, rotating the bearer token: all no-restart operations.
- The admin panel is the contract — anything tunable is reachable from `/admin`.
- The `settings` table is part of every backup.
- Adding a new tunable is a 2-line change (seed + read). Resist the temptation to make everything tunable — overflowing the surface makes the admin panel useless.

## Alternatives considered

- **`appsettings.json` only** — requires restart for every change; awkward for prompts (a Git diff for an experiment is overhead).
- **JSON file watched on disk** — works, but writing from the admin panel to a file mounted into the container is fiddly under Docker, and concurrent writes need locking.
- **Hybrid: secrets in env, tunables in DB** — cleaner security boundary, two surfaces to touch. We landed on DB for everything with `is_secret` as the dimension that matters; the encryption key is the only env-var-only secret. Revisit if the secret surface grows.
- **etcd / Consul** — overkill for a single-node homelab app.

# Backup and Restore

Two things must survive a host loss: the Postgres database and the file volume (`/data`). Everything else is rebuildable from the registry image and the Git repo.

## What's where

| Data                       | Location                                  | Backed up by   |
| -------------------------- | ----------------------------------------- | -------------- |
| App data, vectors, settings, llm_calls, queues | `storymark_postgres` volume → Postgres | `pg_dump`    |
| Original EPUBs, covers, extracted sections     | `storymark_files` volume → `/data`     | `tar`        |
| Ollama model weights                           | `storymark_ollama` volume              | optional — re-pullable |

Postgres holds the source of truth for all queryable state. `/data` holds blobs that are expensive to reprocess (large EPUBs) but not strictly irreplaceable if you keep the original files elsewhere.

## Reference backup script

Drop this into your host's cron / systemd timer. Paths are env-driven so the same script ports across hosts.

```bash
#!/usr/bin/env bash
set -euo pipefail

: "${BACKUP_TARGET_DIR:?Set BACKUP_TARGET_DIR (e.g. /mnt/backup/storymark)}"
: "${POSTGRES_CONTAINER:=storymark-postgres}"
: "${POSTGRES_DB:=storymark}"
: "${POSTGRES_USER:=storymark}"
: "${FILES_VOLUME:=storymark_files}"

timestamp=$(date -u +%Y%m%dT%H%M%SZ)
mkdir -p "$BACKUP_TARGET_DIR"

docker exec "$POSTGRES_CONTAINER" pg_dump -Fc -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  > "$BACKUP_TARGET_DIR/storymark-db-$timestamp.dump"

# Tar the file volume by running tar in a throwaway container that mounts it.
docker run --rm \
  -v "$FILES_VOLUME":/data:ro \
  -v "$BACKUP_TARGET_DIR":/backup \
  alpine:3 \
  tar -czf "/backup/storymark-files-$timestamp.tar.gz" -C /data .

# Retention: keep the last 14 daily archives. Adjust as needed.
find "$BACKUP_TARGET_DIR" -name 'storymark-db-*.dump' -mtime +14 -delete
find "$BACKUP_TARGET_DIR" -name 'storymark-files-*.tar.gz' -mtime +14 -delete
```

This is also CC.2 in [`../issues.md`](../issues.md).

## Restore

On a fresh host:

1. Bring the stack up empty: `docker compose up -d postgres ollama`. Wait for Postgres to be healthy.
2. Restore the database:
    ```bash
    cat storymark-db-<timestamp>.dump | docker exec -i storymark-postgres \
      pg_restore --clean --if-exists --no-owner -U storymark -d storymark
    ```
3. Restore `/data`:
    ```bash
    docker run --rm \
      -v storymark_files:/data \
      -v "$PWD":/backup \
      alpine:3 \
      tar -xzf /backup/storymark-files-<timestamp>.tar.gz -C /data
    ```
4. Start the API: `docker compose up -d api`.

Verify by opening `/admin` and confirming books, settings, and call log are intact.

## What the backups won't cover

- The `.env` file. Treat it as a separate secret you keep alongside backups (in an encrypted store like a password manager or `sops`-encrypted file in Git).
- The CI secrets. Stored in the Git host's secret store; export and back up there.
- Ollama model weights. Re-pulled with `ollama pull`; cheaper than backing up tens of GB.

## Test the restore

A backup you've never restored from is a hope, not a backup. Restore into a throwaway compose stack on a different host at least once. Document any deltas.

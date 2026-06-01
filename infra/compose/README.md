# `infra/compose`

Reference Docker Compose stack for Storymark. Three services: `api`, `postgres` (with pgvector), `ollama`.

## Local one-off

`docker-compose.override.yml` is auto-merged locally and builds the `api` image
from source (context = repo root), so no registry is needed.

```bash
cp .env.example .env       # edit before first up
docker compose up -d --build
docker compose logs -f api
```

Verify the stack is healthy (S0.3 acceptance):

```bash
docker compose ps                                    # api + postgres show healthy
curl -fsS http://localhost:8088/api/health           # {"status":"ok",...}
docker compose exec postgres psql -U storymark -d storymark -c 'SELECT 1;'
docker compose exec postgres psql -U storymark -d storymark \
  -c "SELECT extname FROM pg_extension WHERE extname = 'vector';"   # -> vector
docker compose exec ollama ollama list               # lists models (empty until pulled)
```

## Production via Portainer

This stack is intended for Portainer's Git-backed deploy mode. CI pushes the image; CI then calls Portainer's redeploy API. See [`../../docs/deployment/02-portainer-gitops.md`](../../docs/deployment/02-portainer-gitops.md).

## Reverse proxy / auth

Configurable per [`../../docs/deployment/03-reverse-proxy-options.md`](../../docs/deployment/03-reverse-proxy-options.md) and [`../../docs/deployment/04-auth-options.md`](../../docs/deployment/04-auth-options.md). The `api` service's labels and the `STORYMARK_PROXY_NETWORK` env are the integration surface; everything else stays the same regardless of which proxy/auth you pick.

## Pulling Ollama models

After the stack is up, pull the models you want to use:

```bash
docker exec -it storymark-ollama ollama pull llama3.2:3b
docker exec -it storymark-ollama ollama pull llama3.2:1b
docker exec -it storymark-ollama ollama pull nomic-embed-text
```

The model the admin panel selects must already be pulled here — Storymark does not
pull on demand. A book whose embedding model isn't present fails fast to `Failed`
with a "model not available" reason in the library (run the matching `ollama pull`,
then requeue the job from `/hangfire`).

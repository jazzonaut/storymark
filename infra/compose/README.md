# `infra/compose`

Reference Docker Compose stack for Storymark. Three services: `api`, `postgres` (with pgvector), `ollama`.

## Local one-off

```bash
cp .env.example .env       # edit before first up
docker compose up -d
docker compose logs -f api
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

Or set them via the admin panel at `/admin` — the API will pull on first use if the model isn't cached.

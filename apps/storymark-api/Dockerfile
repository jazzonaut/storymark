# syntax=docker/dockerfile:1
#
# Build context is the repository root (see .github/workflows/ci.yml and
# docs/deployment/01-ci-cd.md): build with
#   docker build -f apps/storymark-api/Dockerfile -t storymark-api .
#
# The image carries both the API and the built SPA (ADR-0013): the API serves
# the Vue app as static files on the same origin, so the deployment is one
# container and one reverse-proxy route.

FROM node:24-slim AS web
WORKDIR /web
RUN corepack enable && corepack prepare pnpm@10.0.0 --activate
# Manifests first so the install layer caches across source-only changes.
COPY pnpm-workspace.yaml pnpm-lock.yaml package.json tsconfig.base.json ./
COPY apps/storymark-app/package.json apps/storymark-app/
COPY packages/shared-types/package.json packages/shared-types/
RUN pnpm install --frozen-lockfile
COPY apps/storymark-app/ apps/storymark-app/
COPY packages/shared-types/ packages/shared-types/
# Only the app emits artifacts; shared-types' build is a typecheck (CI's job).
# vite bundles shared-types' source via the workspace import.
RUN pnpm --filter "./apps/storymark-app" run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/storymark-api/global.json apps/storymark-api/nuget.config ./
COPY apps/storymark-api/Directory.Build.props apps/storymark-api/Directory.Packages.props ./
COPY apps/storymark-api/Storymark.Api.csproj ./
RUN dotnet restore Storymark.Api.csproj
COPY apps/storymark-api/. ./
RUN dotnet publish Storymark.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# curl backs the Compose healthcheck (infra/compose/docker-compose.yml).
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
COPY --from=web /web/apps/storymark-app/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Book artifacts live under /data (a mounted volume). Own the mountpoint as the
# non-root app user so it can create the storage tree; an empty named volume
# inherits this ownership on first mount. A pre-existing root-owned volume must
# be recreated or chowned (see docs/deployment).
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data
USER $APP_UID
ENTRYPOINT ["dotnet", "Storymark.Api.dll"]

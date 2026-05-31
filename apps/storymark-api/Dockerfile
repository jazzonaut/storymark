# syntax=docker/dockerfile:1
#
# Build context is the repository root (see .github/workflows/ci.yml and
# docs/deployment/01-ci-cd.md): build with
#   docker build -f apps/storymark-api/Dockerfile -t storymark-api .

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
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Storymark.Api.dll"]

# `apps/storymark-api`

The ASP.NET Core API — FastEndpoints, EF Core, Hangfire, pgvector retrieval, Ollama / OpenAI-compatible LLM adapters.

**Status:** scaffolded (issue [`S0.2`](../../docs/issues.md)). Targets `net10.0`
(latest LTS). FastEndpoints, EF Core (Npgsql, no migrations yet), and Serilog
(console JSON) are wired. `GET /api/health` returns `{ "status", "version" }`.

## Layout

```text
apps/storymark-api/
  global.json                 # SDK pin (10.0, rollForward latestFeature)
  nuget.config                # restores from nuget.org only (reproducible/CI)
  Directory.Build.props       # nullable, warnings-as-errors, analyzers
  Directory.Packages.props    # central package management, exact-pinned
  .editorconfig               # .NET analyzer severity tuning
  Dockerfile                  # multi-stage, non-root; build context = repo root
  Program.cs                  # Serilog bootstrap, FastEndpoints, DbContext
  appsettings.json / appsettings.Development.json
  Endpoints/Health/HealthEndpoint.cs
  Infrastructure/Database/StorymarkDbContext.cs
```

Folders from the plan (`Services/`, `Jobs/`, `Prompts/`, `Domain/`, …) land with
the features that need them.

## Run

```bash
# local (serves http://localhost:5080 via Properties/launchSettings.json)
dotnet run

# container (build context is the repo root)
docker build -f apps/storymark-api/Dockerfile -t storymark-api .
docker run --rm -p 8080:8080 storymark-api
# both serve GET /api/health
```

CI gates: `dotnet format --verify-no-changes` and `dotnet build -warnaserror`.

See [`docs/conventions/03-dotnet.md`](../../docs/conventions/03-dotnet.md) for the .NET rules.

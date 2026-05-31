# `apps/storymark-api`

The ASP.NET Core API — FastEndpoints, EF Core, Hangfire, pgvector retrieval, Ollama / OpenAI-compatible LLM adapters.

**Status:** placeholder. To be scaffolded by issue [`S0.2`](../../docs/issues.md) in [`docs/issues.md`](../../docs/issues.md).

When scaffolded this directory will contain:

```text
apps/storymark-api/
  Storymark.Api.csproj
  Storymark.Api.sln
  global.json             # SDK version pin
  Directory.Build.props   # nullable, warnings-as-errors, analyzers
  Directory.Packages.props# central package management, exact-pinned
  Dockerfile
  Program.cs
  appsettings.json
  appsettings.Development.json
  Endpoints/
  Domain/
  Infrastructure/
  Services/
  Jobs/                   # Hangfire
  Prompts/                # default prompt text, seeded into settings table
  Storage/
  Data/
  Migrations/
```

See [`docs/conventions/03-dotnet.md`](../../docs/conventions/03-dotnet.md) for the .NET rules.

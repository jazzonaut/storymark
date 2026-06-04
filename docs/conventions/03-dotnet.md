# .NET Conventions

.NET (latest LTS), C# latest. ASP.NET Core, FastEndpoints, EF Core, Hangfire.

## Project-level gates

`apps/storymark-api/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Meziantou.Analyzer" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

`apps/storymark-api/Directory.Packages.props` central-manages package versions; all `<PackageReference>` entries in `.csproj` files omit `Version=`. Versions are exact-pinned, refreshed deliberately.

## Code-quality

- `Nullable=enable`. Use `?` for nullable reference types; never `!` to silence the analyzer.
- `TreatWarningsAsErrors=true`. Don't bypass with `<NoWarn>` — fix the warning or open a tracked suppression.
- **Meziantou.Analyzer** from day one. Suppress only at the call site, never globally, with a reason in the suppression comment.
- `dotnet format --verify-no-changes` is a CI gate. Run `dotnet format` before commit (Taskfile target: `task lint`).
- **DI validation**: `ValidateOnBuild = true; ValidateScopes = true;` in Development. Catches missing registrations and captive dependencies.

## Style

- **File-scoped namespaces** everywhere.
- **One public type per file.** Filename = type name.
- **`required`** on init-only properties that must be supplied. Use it; constructors are usually overkill for DTOs.
- **`record`** for immutable DTOs. `class` for services and entities.
- **`readonly` fields** by default. Same rule as TypeScript: default to immutable, justify mutation.
- **`internal`** by default; `public` only at deliberate API boundaries.
- **Primary constructors** on services where they reduce noise — but no field-with-different-name-than-parameter dance. If you'd need a captured field, use a normal constructor.
- **`var`** for local variables when the type is obvious from the right-hand side; explicit type otherwise.

## Async

- `Task`/`ValueTask` returning methods are suffixed `Async`.
- No `.Result`, `.Wait()`, `GetAwaiter().GetResult()` anywhere.
- `CancellationToken` is a required parameter on every async public method. Plumb it through.
- `ConfigureAwait(false)` is **not** required (ASP.NET Core has no captured context). Don't add it; Meziantou's MA0004 is suppressed for this reason.

## Architecture

- **Layering**: `Endpoints` → `Services` → `Infrastructure`. `Endpoints` MUST NOT reach into `Infrastructure` directly. `Domain` types are pure (no DI, no IO).
- One architecture rule enforced from Phase 0: `Domain` has no dependency on any other project. More rules added when there's enough code to need them (per [ADR-0012](../adr/0012-tests-deferred.md), this currently rides on code review until tests come back).
- **No service locator** (`IServiceProvider.GetService`). Inject what you need.
- **Background jobs** are Hangfire-scheduled and live in `Jobs/`. They orchestrate; they don't contain logic that other call sites would want.

## EF Core

- **No lazy loading.** `Include()` explicitly.
- **No tracking** for read-only queries (`AsNoTracking()`).
- **Compiled queries** for hot reads, post-MVP.
- Migrations are checked in; never edit a migration that has been applied to any environment.
- DbContext is scoped; do not capture it in singletons or background jobs without resolving from the scope.

## Logging

- `ILogger<T>`. Structured logging — never string-interpolate values into the message.
- Log levels: `Trace` (chatty, off in prod), `Debug` (local only), `Information` (lifecycle events), `Warning` (recoverable), `Error` (failed user action or unhandled), `Critical` (process should restart).
- Don't log secrets. Passwords, the settings encryption key, and user-pasted API keys are off-limits.

## FastEndpoints

- One endpoint per file, named for the verb + resource (`UploadBookEndpoint.cs`, `GetBookEndpoint.cs`).
- Routes follow REST: `/api/books`, `/api/books/{id}`, `/api/books/{id}/sections`.
- Request and response DTOs are records, defined inside the endpoint file unless reused across endpoints (then they live in `Domain/<area>/Contracts.cs`).
- Validation via FastEndpoints' integrated validator. No `if (model.X == null)` checks for what the validator should catch.

## What we don't do

- **AutoMapper.** Hand-write mappings; they're a few lines and obvious. No magic.
- **MediatR for everything.** Endpoints can call services directly. We'll reach for it when a fan-out is actually needed.
- **Repositories that wrap `DbContext`** for the sake of it. `DbContext` is already a Unit of Work + repository. Wrap only when there's logic worth encapsulating.

---
name: sammlarr-dev
description: Cheat sheet for working in the Sammlarr repo — build, test, format, run, and SDK/tooling caveats. Use when the user asks how to run tests/build/format, sees an SDK version mismatch, or starts work without context on repo tooling.
---

# Sammlarr dev cheat sheet

Sammlarr is a .NET Blazor Server app (UI/API/Core/DB/Contracts/Shared layered architecture) orchestrated by a .NET Aspire AppHost. Use the commands below from the repo root (`C:/Repos/Sammlarr`).

## Build, test, run

| Task | Command |
| --- | --- |
| Restore | `dotnet restore` |
| Build | `dotnet build` |
| Run all tests | `dotnet test` |
| Run a single test project | `dotnet test Tests/API/API.Tests.csproj` |
| Run the app | `aspire run` |
| Run the app with hot reload | `aspire run --watch` |
| Format changed files | `dotnet csharpier format <paths>` |
| Format everything | `dotnet csharpier format .` |

The solution is `Sammlarr.slnx` (the XML solution format); `dotnet` auto-detects it, so no filename is needed on `restore`/`build`/`test`.

## Running the app — single entry point

The **Aspire AppHost (`Sammlarr.AppHost`) is the only way to run the app.** It orchestrates the `UI` resource and brings up the Aspire dashboard (logs/metrics/traces). Don't run `UI` directly — its `launchSettings.json` exists only to supply the endpoint binding Aspire reads.

- **CLI:** `aspire run` (or `aspire run --watch` for the hot-reload dev loop) from the repo root — auto-detects the AppHost, no `--project`.
- **Rider:** the `AppHost` run config (`.run/AppHost.run.xml`) — F5 to debug.
- Telemetry (`OTEL_EXPORTER_OTLP_ENDPOINT`, `Sammlarr__Telemetry__*`) is injected into `UI` by the AppHost; the custom `Shared/Telemetry` module reads it. `compose.yaml` is a separate self-host/deploy artifact (UI container + standalone dashboard), not part of the dev loop.

The app uses SQLite (`sammlarr.db` auto-created via `Database.EnsureCreated()` + column migrations in `DB/SammlarrDbContext.cs`). There are no EF migrations — schema evolution happens in `MigrateListColumns()` with `ALTER TABLE` + try/catch.

## SDK pinning

`global.json` pins a specific .NET 10 preview SDK. If `dotnet build` errors with SDK-not-found, the user needs to install the exact SDK version listed in `global.json` — do NOT edit `global.json` to downgrade.

## Test framework

Tests use **TUnit** (not xUnit/NUnit). Key differences:
- `[Test]` instead of `[Fact]`, `[Arguments(...)]` instead of `[Theory]/[InlineData]`.
- Assertions are async: `await Assert.That(x).IsEqualTo(y);`
- Mocks via `TUnit.Mocks`; HTTP stubs via **Imposter** (`Mock.HttpHandler()`).
- Fake data via **Bogus** (`new Faker()`).

See the `write-tunit-test` skill for templates.

## Formatter

**CSharpier** (`dotnet-tools.json`, v1.2.6) formats with tabs, `printWidth=160`. Always run `dotnet csharpier format <changed files>` before committing C# changes. Never hand-format — CSharpier will rewrite it.

## Code conventions (enforced)

- **No XML doc comments** (`/// <summary>`). Self-documenting code only; short non-XML comments for non-obvious intent.
- PascalCase public, `_camelCase` private fields, `I`-prefixed interfaces.
- `var` only when the type is apparent.
- Nullable reference types on everywhere.
- Async everywhere; propagate `CancellationToken`.
- Access modifier: prefer most restrictive that fits.
- Records / record structs for immutable DTOs.

## No defensive programming; readability > performance

Explicit user preference. Applies everywhere.

- **No try/catch walls.** Catch only when (a) there's a real recovery plan different from crashing, or (b) it's a genuine external boundary (HTTP, file I/O, untrusted input) AND the exception type is specific. Never `catch (Exception)` to log-and-rethrow — let it propagate.
- **No null guards on trusted callers.** Nullable reference types already enforce nullability statically. `ArgumentNullException.ThrowIfNull` only at public API boundaries; inside private helpers called by trusted code, it's noise.
- **No "just in case" validation.** Don't re-validate data that's already been validated upstream.
- **No `if (x == null) return;`** defensive early exits unless null is a meaningful domain state. If it's semantically "never null", model it as non-nullable.
- **Flat over nested.** Use early-return guards at the top; keep the happy path unindented. If nesting crosses ~3 levels, extract a helper.
- **Readability beats micro-optimization.** Prefer clear LINQ to hand-rolled loops, simple property access to cached locals, plain `await` paths. The EF + HTTP layers in this repo are not hot paths — clarity wins there. Optimize only with a profiler trace in hand.
- **Benchmark.** The existing `SammlarrDbContext.MigrateListColumns` try/catch is the bar: a concrete, reproducible SQLite "column already exists" condition with no other handling option. That shape is fine; "wrap this in try/catch to be safe" is not.
- **When editing existing code**, delete defensive code you encounter — log-and-rethrow blocks, null-checks that contradict NRT annotations, redundant validators — unless removing them changes observable behavior.

## Layering

Respect the dependency direction: `Contracts ← Shared ← {Core, DB, API} ← UI`.
- EF / `DbContext` lives **only** in `DB/`.
- HTTP clients for external providers live **only** in `API/`.
- Cross-layer DTOs + interfaces + enums live in `Contracts/`.
- `UI` composes but doesn't implement protocol or persistence.

## DI composition

Every subsystem exposes an `IServiceModule`. Register via `services.AddModule<TModule>()` — see `UI/Program.cs`. When adding a new service, add it inside the appropriate module's `RegisterServices`, don't register it at the call site.

For provider-specific implementations (e.g., multiple `IMediaRecommendationService`s), use **keyed** registrations: `AddKeyedScoped<IFoo, FooImpl>(ProviderEnum.X)`.

## Options

Configuration types implement `IOptionsSection` and get registered via the `Shared.Options.Generator` Roslyn source generator. Add a new `record XOptions : IOptionsSection` in `Shared/Options/` — the generator handles binding/registration automatically. Do not call `services.Configure<XOptions>` by hand.

## Authoritative guidance docs (`.github/instructions/`)

These are the source of truth for conventions. Read them when scope is ambiguous:

| File | Applies to | Use when |
| --- | --- | --- |
| `architecture.instructions.md` | all files | Placing code in the right project, DI/options patterns, installed-library list. |
| `csharp.instructions.md` | `**/*.cs` | C# style: naming, nullability, records, no XML doc comments. |
| `blazor.instructions.md` | `**/*.razor*` | Component structure, lifecycle hooks, state management, no XML doc comments. |
| `dotnet-architecture-good-practices.instructions.md` | all .cs/.razor/.csproj | Generic DDD guidance. **Partially inaccurate for this repo** — its test template uses xUnit (`[Fact]`, `Assert.Equal`), but Sammlarr uses TUnit. Ignore the xUnit snippets and the financial-domain sections; the SOLID / layering / ubiquitous-language parts are fine. |
| `git-commit-instructions.md` | commits | See the `git-commit` skill. |

## Installed libraries (per `architecture.instructions.md`)

- `Microsoft.Extensions.*` — DI, Caching.Memory, Options.ConfigurationExtensions.
- `Microsoft.EntityFrameworkCore.Sqlite` / `InMemory` — persistence + testing.
- `Microsoft.AspNetCore.DataProtection` — protects sensitive data and keys.
- `Vogen` — strongly-typed IDs / value objects (used in `Contracts`/`Shared`).
- `BlazorBlueprint.*` + `timewarp-simple-icons` — UI components and icons.
- `TUnit` (`.Core`, `.Assertions`, `.Mocks`, `.Mocks.Http`) — test framework.
- `Bogus`, `Imposter` — test data generation + HTTP stubbing.
- `Dapper` — lightweight data access in some test projects.
- `Microsoft.CodeAnalysis.*` — Roslyn, used by `Shared.Options.Generator`.

Check `Directory.Packages.props` for current versions before adding a new package — the repo uses central package management.

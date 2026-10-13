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
| Run with watch / hot reload | `dotnet watch --project AppHost` (or Aspire watch mode — see "Hot reload / watch") |
| Format changed files | `dotnet csharpier format <paths>` |
| Format everything | `dotnet csharpier format .` |

The solution is `Sammlarr.slnx` (the XML solution format); `dotnet` auto-detects it, so no filename is needed on `restore`/`build`/`test`.

## Running the app — single entry point

The **Aspire AppHost (`AppHost`) is the way to run the full app.** It orchestrates the `UI` resource and brings up the Aspire dashboard (logs/metrics/traces). The UI is pinned to a fixed dev URL **http://localhost:6769** (`AppHost/AppHost.cs` `WithHttpEndpoint`), and the AppHost's `launchSettings.json` bakes `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, so a bare `aspire run` starts cleanly (no https `applicationUrl` crash).

- **CLI:** `aspire run` from the repo root — auto-detects the AppHost, no `--project`. UI → http://localhost:6769, dashboard → http://localhost:15223. (There is **no** `aspire run --watch` flag — see "Hot reload / watch" for the real options.)
- **Rider:** the auto-generated **`AppHost: http`** Aspire run config — F5 to run/debug. Rider derives it from the `<IsAspireHost>` project + its `http` launch profile; there is **no** checked-in `.run/*.xml`.
- **UI only:** the `UI: http` launch profile runs the UI standalone (also on :6769) for pure-UI work. It and the AppHost both bind :6769 — run one or the other. (The UI project has a single `http` launch profile — Aspire reads its `applicationUrl`; there is intentionally no hand-rolled `watch` profile.)
- **Radarr + Sonarr beside it (opt-in):** `aspire run -- --RadarrSonarr=true` also starts pinned `lscr.io/linuxserver` Radarr (:7878) and Sonarr (:8989) containers (`AppHost/ArrContainers.cs`), each from a fresh copy of `AppHost/arr/<app>/config.xml` (test API key `0123456789abcdef0123456789abcdef`, sign-in off for local addresses) with `/movies` and `/anime-movies` (Radarr) and `/tv` and `/anime` (Sonarr) registered as root folders, the default one first, once they answer `/ping`. Their state lives in `%TEMP%\sammlarr-arr` and is wiped on every start. Point Settings › Providers › Wantlist sync at `http://localhost:7878` and `http://localhost:8989`.
- **Staging (a seeded install beside dev):** `dotnet run --project AppHost -c Staging --launch-profile staging` after `dotnet user-secrets set Parameters:tmdb-api-key <key> --project AppHost`. The `Staging` configuration builds into `bin\Staging`, so it never locks the dev build. Sammlarr runs from the `Web/Dockerfile` image at http://staging.localhost:6789 (`admin` / `admin`, `member` / `member`; personas, ports and the setup code live in `AppHost/appsettings.Staging.json`), dashboard http://localhost:15224, Radarr :7879, Sonarr :8990. The Restage, Reset and Rebuild commands sit on the `sammlarr` resource in the dashboard (or `aspire resource sammlarr restage` while staging is the only AppHost running); Radarr, Sonarr and WireMock stay up as persistent containers. Full description: `docs/architecture/testing.md` › Staging.
- Telemetry (`OTEL_EXPORTER_OTLP_ENDPOINT`, `Sammlarr__Telemetry__*`) is injected into `UI` by the AppHost; the custom `Shared/Telemetry` module reads it. The self-host compose is generated, not hand-written: `aspire publish` writes it (`aspire-output/docker-compose.yaml`, naming the released GHCR image), and each release attaches it.

**Agent live-inspect** (only when the user asks or a task needs it — otherwise let the user run their own session; never auto-start on edits): build (`mcp__rider__build_solution` / `dotnet build`) → ensure `:6769` is free → start (`AppHost: http` via `mcp__rider__execute_run_configuration`, or `aspire run`) → drive Chrome at **http://localhost:6769** → errors via `mcp__rider__get_file_problems` + the Aspire dashboard in Chrome (http://localhost:15223 — Console/Structured/Traces). Stop by killing the `AppHost`/`dcp`/`UI` tree — **never blanket-kill `aspire`** (kills the Aspire MCP) or `rider64.exe`. On this toolchain the Aspire MCP does not detect the AppHost, so don't rely on `mcp__aspire__list_resources`/`list_console_logs`.

**Hot reload / watch.** Watch is driven at the **AppHost level**, never via a per-project `dotnet watch` launch profile (that bypasses Aspire orchestration — anti-pattern). Options, best-first:
- **Rider Debug** — run `AppHost: http` in Debug; .NET Hot Reload applies most `.razor`/`.cs` edits live. Rude edits (new `await`/`yield`, renamed params, removed `[Parameter]` attributes) still need a restart.
- **Aspire watch mode** (restart-based, opt-in per developer) — enable globally for yourself with `aspire config set -g features.defaultWatchEnabled true`, then plain `aspire run` restarts the app graph on AppHost / C#-project changes. It's a feature flag with quirks (some edits still need a manual restart) — prefer Rider Debug. **Don't** set it repo-locally: a committed `.aspire/settings.json` would force restart-based watch on everyone (incl. agents/CI) and break deterministic `aspire run`. (`aspire run` has **no** `--watch` flag in CLI 13.3.0.)
- **`dotnet watch --project AppHost`** — the .NET SDK watch loop; it transitively watches the AppHost and its C# project resources (incl. `UI`). Has quirks — some edits still need an explicit restart; when a change doesn't apply, restart the resource rather than fighting the watcher.

**No browser auto-refresh.** A Rider or `aspire run` launch does not inject `dotnet watch`'s `aspnetcore-browser-refresh.js`, and the app wires no live-reload middleware (`Westwind.AspNetCore.LiveReload` was removed on 2026-09-30 because it did not work). Reload the page yourself after a change; `.razor` markup and scoped CSS still need a rebuild, and a new Tailwind utility needs `app.css` regenerated first.

The app uses SQLite (`sammlarr.db`) with **EF Core migrations** applied via `Database.Migrate()` at startup — change the model, then `dotnet ef migrations add <Name>`.

## SDK pinning

`global.json` pins a specific .NET 11 preview SDK. If `dotnet build` errors with SDK-not-found, the user needs to install the exact SDK version listed in `global.json` — do NOT edit `global.json` to downgrade.

## Test framework

Tests use **TUnit** (not xUnit/NUnit). Key differences:
- `[Test]` instead of `[Fact]`, `[Arguments(...)]` instead of `[Theory]/[InlineData]`.
- Assertions are async: `await Assert.That(x).IsEqualTo(y);`
- Mocks via `TUnit.Mocks`; HTTP stubs via **TUnit.Mocks.Http** (`Mock.HttpHandler()`).
- Fake data via **Bogus** (`new Faker()`).

See the `write-tunit-test` skill for templates.

## Formatter

**CSharpier** (`dotnet-tools.json`, v1.3.0; it cannot parse `union` or `closed` yet, so `.csharpierignore` lists those files until a release ships belav/csharpier#1888) formats with tabs, `printWidth=160`. Always run `dotnet csharpier format <changed files>` before committing C# changes. Never hand-format: CSharpier will rewrite it.

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
- **No `if (x == null) return;`** defensive early exits. A meaningful absence is an `Optional`, not a null (docs/architecture §06, "Absence and failure"); if it's semantically "never null", model it as non-nullable. A `T?` a framework hands in (a route or query parameter, an EF row) may still be null-checked where it enters. Read an `Optional` or a `Result` through its helpers; an early-return guard on one stays only where the code after it holds a method-scoped `using var` or a `try`, or where the guard itself leaves a loop (`continue`/`break`).
- **Flat over nested.** Use early-return guards at the top; keep the happy path unindented. If nesting crosses ~3 levels, extract a helper.
- **Readability beats micro-optimization.** Prefer clear LINQ to hand-rolled loops, simple property access to cached locals, plain `await` paths. The EF + HTTP layers in this repo are not hot paths — clarity wins there. Optimize only with a profiler trace in hand.
- **Benchmark.** A try/catch earns its place only around a concrete, reproducible failure that has no other handling option, caught by its exact type. "Wrap this in try/catch to be safe" never does.
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
- `BlazorBlueprint.*`: UI components and icons. Provider logos are vendored files in `UI/wwwroot/providers/`, drawn by `ProviderLogo` and `ProviderTile`.
- `TUnit` (`.Core`, `.Assertions`, `.Mocks`, `.Mocks.Http`) — test framework.
- `Bogus`, `TUnit.Mocks.Http` — test data generation + HTTP stubbing.
- `Dapper` — lightweight data access in some test projects.
- `Microsoft.CodeAnalysis.*` — Roslyn, used by `Shared.Options.Generator`.

Check `Directory.Packages.props` for current versions before adding a new package — the repo uses central package management.

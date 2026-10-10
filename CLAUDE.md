# Sammlarr

A Blazor Server media-list curator that aggregates Plex / TMDB / IMDb / TheTVDB / MDBList into curated lists.

## Architecture

Layered .NET solution; dependencies point down toward the kernel (`Shared`). The full
building-block view (projects, dependency graph, per-layer roles) and the cross-cutting concepts
(DI, providers, resilience, persistence, auth, observability) live in `docs/architecture`
(sections 3 and 6). Read those rather than duplicating them here.

DI uses a module pattern: each layer and provider ships an `IServiceModule` registered from
`Web/Program.cs` via `AddModule<T>()`; a provider module registers its clients and roles through
`ForRemote<TOptions>(RemoteService)` (`API/Http`), keyed off the hand-written role enums in `Domain/Providers`.

## UI

Use **BlazorBlueprint** components for everything in `UI/` unless explicitly asked otherwise. Reach for the `mcp__blazorblueprint__*` tools (`list_components`, `search_components`, `get_component`, `get_primitive`, `get_patterns`, `get_icon_library`, …) before hand-rolling markup or pulling in another library.

## Testing

**TUnit** everywhere, run on Microsoft.Testing.Platform via `dotnet run --project Tests/<X> --no-build` (not `dotnet test`; Verify snapshots require the native runner). Tiers: `Tests/{Domain,Shared}` pure unit (+ CsCheck laws); `Tests/Core` single-collaborator units (`IFoo.Mock()`); `Tests/API` provider clients (`TUnit.Mocks.Http`); `Tests/Integration` full-graph behavior + repository tests on the real-SQLite **`SammlarrTestHost`** harness; `Tests/UI` bUnit component tests; `Tests/Web` the Web host's own code (redirects, health checks, the staging seed), the only non-E2E tier that builds Web and needs Node. Assert unions/results with the `TestInfrastructure` extensions (`.IsSuccess`/`.IsError`/`.IsSome`/`.IsNone`/`.IsOfUnionType<,>`), never `Assert.That(x is Case).IsTrue()`. `Tests/E2E` Playwright flows, one isolated Aspire+WireMock+SQLite stack leased per test. Assert unions/results with the `TestInfrastructure` extensions (`.IsSuccess`/`.IsError`/`.IsSome`/`.IsNone`/`.IsOfUnionType<,>`), never `Assert.That(x is Case).IsTrue()`. Every test file must be analyzer-clean. Overview of the tiers, the per-tier run commands, the E2E record/replay model, the env vars and the Stryker audit: **`docs/architecture/testing.md`**. Full how-to (tier placement, harness seed helpers, mocks, CsCheck, Verify, bUnit) in the **`write-tunit-test`** skill.

## System

Windows 11. Use **PowerShell** for shell commands — never bash, sh, git-bash, or WSL. No Python.

CSharpier runs automatically on `.cs` edits via a PostToolUse hook; don't invoke it manually.

## Styling

Tailwind utilities come from **`Web/wwwroot/css/app.css`**, which is **generated, gitignored, and rebuilt by `dotnet build`** (the `BuildTailwindCss` target in `Web/Web.csproj` runs `npm run css`, and `npm ci` first if `node_modules` is missing). A new utility class works as soon as you build; run `npm run css` by hand for a faster loop. This means **`dotnet build` requires Node**.

Custom CSS has a home per kind, all in `Web/wwwroot/css/app-input.css`: design tokens in the `@config`'d `UI/tailwind.config.js`, component classes in `@layer components`, custom utilities via `@utility`, and `@source inline("…")` to safelist classes the scanner cannot see because they are assembled in C#. Anything component-specific stays in that component's scoped `.razor.css` — note a class passed into a child component (`<LucideIcon Class="mr-2"/>`) or portalled content lands outside the parent's scope, so those need a utility rather than scoped CSS.

## Dev run

The app runs via the Aspire AppHost (`AppHost/`). The UI is pinned to a fixed dev URL **http://localhost:6769** (`AppHost/AppHost.cs` `WithHttpEndpoint`), and the AppHost's `launchSettings.json` bakes `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, so a bare `aspire run` starts without the https crash. Aspire dashboard: **http://localhost:15223**.

Default: **let the user run their own session** — never auto-start on edits or spin up a competing instance. When the user explicitly asks (or a task genuinely needs live inspection), the supported flow is: build (`mcp__rider__build_solution` or `dotnet build`) → make sure `:6769` is free (`Get-NetTCPConnection -LocalPort 6769`; kill a stale owner first) → start via the Rider **`AppHost: http`** run config (`mcp__rider__execute_run_configuration`) or `aspire run` → drive Chrome at **http://localhost:6769** (fixed, no lookup) → read errors via `mcp__rider__get_file_problems` + build output. Restart = rebuild + relaunch.

Stop by killing the `AppHost`/`dcp`/`UI` process tree — **never blanket-kill `aspire`** (that also kills the Aspire MCP server) and never `rider64.exe`. The old `dotnet run --project UI --urls … --no-build` + `Stop-Process` port-kill is deprecated. Note: on this toolchain the Aspire MCP does **not** detect the AppHost (neither CLI- nor Rider-launched), so use the fixed `:6769` + the dashboard-in-Chrome for resource state/logs — not `mcp__aspire__list_resources`/`list_console_logs`. Full playbook in the `sammlarr-dev` skill.

# Sammlarr

A Blazor Server media-list curator that aggregates Plex / TMDB / IMDb / TheTVDB / MDBList into curated lists.

## Architecture

Layered .NET solution; dependencies point down toward the kernel (`Shared`). The full
building-block view (projects, dependency graph, per-layer roles) and the cross-cutting concepts
(DI, providers, resilience, persistence, auth, observability) live in `docs/architecture`
(sections 3 and 6). Read those rather than duplicating them here.

DI uses a module pattern: each layer and provider ships an `IServiceModule` registered from
`UI/Program.cs` via `AddModule<T>()`; providers register as keyed services keyed off the
`MediaProvider` enums in `Domain/Providers`.

## UI

Use **BlazorBlueprint** components for everything in `UI/` unless explicitly asked otherwise. Reach for the `mcp__blazorblueprint__*` tools (`list_components`, `search_components`, `get_component`, `get_primitive`, `get_patterns`, `get_icon_library`, …) before hand-rolling markup or pulling in another library.

## System

Windows 11. Use **PowerShell** for shell commands — never bash, sh, git-bash, or WSL. No Python.

CSharpier runs automatically on `.cs` edits via a PostToolUse hook; don't invoke it manually.

## Dev run

The app runs via the Aspire AppHost (`AppHost/`). The UI is pinned to a fixed dev URL **http://localhost:6769** (`AppHost/AppHost.cs` `WithHttpEndpoint`), and the AppHost's `launchSettings.json` bakes `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, so a bare `aspire run` starts without the https crash. Aspire dashboard: **http://localhost:15223**.

Default: **let the user run their own session** — never auto-start on edits or spin up a competing instance. When the user explicitly asks (or a task genuinely needs live inspection), the supported flow is: build (`mcp__rider__build_solution` or `dotnet build`) → make sure `:6769` is free (`Get-NetTCPConnection -LocalPort 6769`; kill a stale owner first) → start via the Rider **`AppHost: http`** run config (`mcp__rider__execute_run_configuration`) or `aspire run` → drive Chrome at **http://localhost:6769** (fixed, no lookup) → read errors via `mcp__rider__get_file_problems` + build output. Restart = rebuild + relaunch.

Stop by killing the `AppHost`/`dcp`/`UI` process tree — **never blanket-kill `aspire`** (that also kills the Aspire MCP server) and never `rider64.exe`. The old `dotnet run --project UI --urls … --no-build` + `Stop-Process` port-kill is deprecated. Note: on this toolchain the Aspire MCP does **not** detect the AppHost (neither CLI- nor Rider-launched), so use the fixed `:6769` + the dashboard-in-Chrome for resource state/logs — not `mcp__aspire__list_resources`/`list_console_logs`. Full playbook in the `sammlarr-dev` skill.

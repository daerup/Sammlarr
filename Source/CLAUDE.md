# Sammlarr

A Blazor Server media-list curator that aggregates Plex / TMDB / IMDb / TheTVDB / MDBList into curated lists.

## Architecture

Layered: `Contracts ← Shared ← {Core, DB, API} ← UI`

| Project      | Role                                                                            |
|--------------|---------------------------------------------------------------------------------|
| `Contracts/` | Interfaces, enums, record-struct IDs, DI markers                                |
| `Shared/`    | Options records, extension methods, `AddModule<T>()` infra                      |
| `Core/`      | Business logic, blacklist, query/cache                                          |
| `DB/`        | `SammlarrDbContext`, entities, repositories                                     |
| `API/`       | Per-provider folders — HTTP clients + keyed DI                                  |
| `UI/`        | Blazor Server; `Features/{Auth,Blacklist,Browse,Feeds,Home,Settings,Watchlist}` |
| `Tests/`     | Mirrored per layer                                                              |

DI uses a module pattern: each layer ships an `IServiceModule` registered from `UI/Program.cs` via `AddModule<T>()`. Providers register as keyed services keyed
off enums in `Contracts/` (`MediaSearchProvider`, `WatchlistProvider`, …).

## UI

Use **BlazorBlueprint** components for everything in `UI/` unless explicitly asked otherwise. Reach for the `mcp__blazorblueprint__*` tools (`list_components`, `search_components`, `get_component`, `get_primitive`, `get_patterns`, `get_icon_library`, …) before hand-rolling markup or pulling in another library.

## System

Windows 11. Use **PowerShell** for shell commands — never bash, sh, git-bash, or WSL. No Python.

CSharpier runs automatically on `.cs` edits via a PostToolUse hook; don't invoke it manually.

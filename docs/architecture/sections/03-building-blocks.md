# 3. Building Block View

Sammlarr is twelve .NET projects in one solution, plus the tests. The idea to take away: dependencies point down
toward the kernel (`Shared`), and the implementations (`Core`, `DB`, `API`) and the presentation (`UI`) never reference
each other; they meet only through the interfaces in `Contracts`. `Web` is the one project that sees them all and
composes them.

## Whitebox Overall System

```mermaid
flowchart TD
  apphost["AppHost<br/>Aspire host"]
  web["Web<br/>host, composition root"]
  ui["UI<br/>Blazor components (RCL)"]
  core["Core<br/>business logic"]
  db["DB<br/>EF Core + SQLite"]
  api["API<br/>one module per service"]
  msg["Messaging<br/>Wolverine, notifications"]
  obs["Observability<br/>Serilog + OpenTelemetry"]
  contracts["Contracts<br/>interfaces"]
  domain["Domain<br/>model"]
  shared["Shared<br/>kernel"]
  roslyn["Roslyn<br/>generators + analyzers"]

  apphost --> web
  web --> ui
  web --> core
  web --> db
  web --> api
  web --> msg
  web --> obs
  ui --> obs
  core --> obs
  db --> obs
  api --> obs
  ui --> contracts
  core --> contracts
  db --> contracts
  api --> contracts
  msg --> contracts
  obs --> contracts
  contracts --> domain
  domain --> shared
  shared -. build time .-> roslyn
```

Arrows are `<ProjectReference>`s. Every project except `Contracts` and `Roslyn` also references `Shared` directly; those edges are
left out. `Roslyn` ships nothing at runtime: the root `Directory.Build.props` adds it to every other project as an
analyzer.

| Project | What it holds |
|---|---|
| `Shared` | The kernel, on no other project: the monads `Optional` and `Result`, `ValueSet`, the options records (`Shared/Options`), the lane primitives (`LaneGate`, `Lane<TKey, TOut>`, `LanePriority`), the burst helper `BurstsAsync`, and the module infrastructure (`IServiceModule`, `AddModule<T>()`). |
| `Domain` | The pure model, on `Shared` only: entities, value objects and unions for media, feeds, the wantlist and blocklist, providers (`RemoteService`, the role enums), jobs and notifications, and the ids (`MediaId`, `FeedId`, `UserId`, one record struct per external id). No infrastructure. See [Cross-cutting Concepts](06-crosscutting.md). |
| `Contracts` | The seams: the service, repository and provider-role interfaces, the records they exchange, and the DI markers (`Keyed`, `Registered`, `ClientOf`). References `Domain` because its signatures use domain types. |
| `Observability` | Serilog and OpenTelemetry setup, the in-memory log capture behind Settings › Diagnostics, the usage metrics, the feed-run spans, and the tracing helpers (`AddTracedScoped`, `AddTracedKeyedScoped`) other projects register through. |
| `Core` | The business logic behind the service interfaces: loading a list and identifying its titles (`Core/Media`), the query processor (filter, sort, limit), feeds and their runs, the wantlist and its sync, the blocklist, the job queue and runner, the TickerQ tickers (feed schedules, dataset loads, housekeeping), the `Announcer`, sign-in, users, setup, ratings and usage reports. |
| `DB` | Persistence in one SQLite file ([ADR 0004](../../adr/0004-store-all-state-in-one-sqlite-file.md)): `SammlarrDbContext`, the row types, the repositories and the EF Core migrations, including the job table, the identity claims (`IdentityClaims`), the wantlist outbox and the usage store; the TickerQ store (`DB/Scheduling`); and the encryption of stored secrets with data protection (`DB/Conversion/Encryption`). |
| `API` | One folder per external service, each with an `IServiceModule` that registers its HTTP clients and the roles it serves, plus the lanes and HTTP pipeline every remote call passes and the webhook and Pushover channels. See [API: one module per provider](#api-one-module-per-provider). |
| `Messaging` | Wolverine with durable queues in the same SQLite file, for notifications only: `NotificationDispatchHandler` fans a notification out to its subscriptions and `NotificationDeliveryHandler` delivers each one, retried with cooldowns ([ADR 0008](../../adr/0008-separate-events-from-notifications.md)). |
| `UI` | The Blazor components as a Razor class library on BlazorBlueprint: one folder per feature under `UI/Features`, shared components and services under `UI/Shared`. References only `Contracts`, `Observability` and `Shared` ([ADR 0002](../../adr/0002-split-presentation-from-the-host.md)). |
| `Web` | The ASP.NET Core host and composition root: the root components (`App`, `Routes`), `Program.cs`, the auth and health endpoints, the generated Tailwind CSS in `wwwroot`, and the `Dockerfile`. |
| `AppHost` | The Aspire host: runs `Web` for development and describes the container deployment ([ADR 0001](../../adr/0001-drive-deployment-from-the-aspire-apphost.md)). See [Deployment View](05-deployment.md). |
| `Roslyn` | Build time only. Source generators for the options registration (types marked `IOptionsSection`), the rating suppliers (`[SuppliedBy]` on `RatingSource`) and the `Task` mirrors of the monad helpers (`[GenerateAsync]`); analyzers `SAM0001` (test names), `SAM0002` and `SAM0003` (an `Optional` or `Result` read by hand, or branched on as a `bool`; see [Absence and failure](06-crosscutting.md#absence-and-failure-optional-result-or-null)). |

The test projects under `Tests/` (one per tier, plus shared harnesses and the architecture and analyzer tests) are
described in [Testing](../testing.md).

The rules that hold the graph together:

- `Core`, `DB` and `API` are siblings: none references another or `UI` (`Tests/Architecture/DependencyTests.cs`
  checks it). To push the wantlist, `Core` calls the `IMediaSink` that `API` implements, resolved by its `SinkKind`.
- `UI` sees abstractions only: it works against the interfaces in `Contracts`, never an implementation.
- `Web` is the only place the implementations meet. `Program.cs` adds one module per layer with `AddModule<T>()`
  (`SharedModule`, `ObservabilityModule`, `DbModule`, `CoreModule`, `ApiModule`, `MessagingModule`, `UiModule`,
  `WebModule`). How modules and keyed services work: [Cross-cutting Concepts](06-crosscutting.md).

## Level 2

Two blocks are worth opening: `API`, where services plug in, and `UI` with `Web`, where features live.

### API: one module per provider

Each service Sammlarr calls has a folder under `API` with one module, added by `ApiModule`; how a module registers its
clients, roles, converter and enricher is in [Cross-cutting Concepts](06-crosscutting.md). Why a service takes every
role its API offers: [ADR 0011](../../adr/0011-give-a-service-every-role-its-api-supports.md).

| Service | Folder (module) | Roles, by role enum member | Converter, enricher | Also registers |
|---|---|---|---|---|
| TMDB | `Tmdb` (`TmdbModule`) | key checks `ApiKeyProvider.Tmdb` and `ApiKeyProvider.TmdbReadToken`; `FeedProvider.Tmdb`; `TrendingProvider.Tmdb`; `SearchProvider.Tmdb` | `TmdbIdConverter`, `TmdbDetailsEnricher` | `TmdbArtwork` (artwork resizer) |
| Plex | `Plex` (`PlexModule`) | sign-in `AuthProvider.Plex`; `SearchProvider.Plex`; sink `SinkKind.Plex` | `PlexIdConverter`, `PlexApi` | `PlexArtwork`, `PlexUrls` |
| MDBList | `Mdblist` (`MdblistModule`) | key check `ApiKeyProvider.Mdblist`; `FeedProvider.Mdblist` | `MdblistIdConverter`, `MdblistEnricher` | `MdblistTitles` (burst primer), `MdblistBudget` (lane quota) |
| Wikidata | `Wikidata` (`WikidataModule`) | none | `WikidataIdConverter` | |
| TVmaze | `Tvmaze` (`TvmazeModule`) | none | `TvmazeIdConverter`, `TvmazeEnricher` | |
| IMDb datasets | `Imdb` (`ImdbModule`) | none | `ImdbFileEnricher` | `ImdbRatingsFile` (dataset), `ImdbRatingsInRun` (burst primer) |
| Anime lists | `Anime` (`AnimeListsModule`) | none | `AnimeListsIdConverter` | `AnimeIdsFile` (dataset), `AnimeIdsInRun` (burst primer), `LaterSeasons` |
| AniList | `AniList` (`AniListModule`) | `FeedProvider.AniList` | `AniListEnricher` | `AniListTitles`, `AniListPrequels` (`IAnimePrequels`) |
| MyAnimeList | `Mal` (`MalModule`) | key check `ApiKeyProvider.Mal`; `FeedProvider.Mal` | `MalEnricher` | |
| Trakt | `Trakt` (`TraktModule`) | key check `ApiKeyProvider.Trakt`; `FeedProvider.Trakt` | `TraktIdConverter`, `TraktEnricher` | `TraktTitles` |
| Radarr, Sonarr | `Arr` (`ArrModule`) | through `ForService`: sink `SinkKind.Arr` (`ArrSink`); `SearchProvider.Arr` (`ArrSearch`) | none | `RadarrApi`, `SonarrApi`, `IArrConnectionTester` |

A dataset is a file loaded daily into a table ([ADR 0013](../../adr/0013-load-shared-datasets-outside-the-job-queue.md)).
Which converter and enricher a title gets: [Provider routing](06-crosscutting.md#provider-routing); what each service
answers: [Context and Scope](02-context-scope.md). Id sources Sammlarr never calls (TheTVDB, Rotten Tomatoes,
Metacritic, Letterboxd) get no client ([ADR 0010](../../adr/0010-split-the-ids-from-the-services-sammlarr-calls.md));
the `Tvdb` folder below only sizes TheTVDB's artwork.

The other folders under `API`:

| Folder | Holds |
|---|---|
| `Http` | The registration builder (`ForRemote`, `ForService`); `AddRemoteHttp`, the laned pipeline of every service client (`Http/Lanes`); `AddClientHttp`, the standard resilience pipeline for the other clients; and shared helpers for answers and key checks (`Answering`, `RecentAnswers`, `CheckAccessAsync`). See [Lanes and HTTP resilience](06-crosscutting.md#lanes-and-http-resilience). |
| `Feeds` | What the feed sources share: `SiteLink` (a site's page link turned into a stored path and query), `FeedPages` and `AnimeSeason`. |
| `Notifications` | The webhook and Pushover channels (`INotificationChannel`, keyed by `NotificationChannel`) and the Fluid template renderer. The web UI channel lives in `UI/Shared/Notifications`. |
| `Tvdb` | `TvdbArtwork`, the artwork resizer for TheTVDB image links. No client. |
| `Pictures` | `PictureGrids` (`IPictureGrids`): reads a person's profile picture and scales it down to a pixel grid the UI paints their avatar from. |
| `GitHub` | `GitHubReleaseFeed` (`IReleaseFeed`): reads the latest published release for the app's update check. |
| `Observability` | `InstrumentationModule`, which traces every `HttpClient` call. |

At the root, `ResilienceModule` adds `HttpModule` (the lanes and `IRemoteCapacity`) and sets the log level of
resilience events.

### UI and Web: feature slices, and the host that composes them

`UI/Features` has one folder per feature area, each a vertical slice of pages and components:

| Folder | Routes | What it is |
|---|---|---|
| `Home` | `/` | The dashboard. |
| `Browse` | `/browse` | Trending titles. |
| `Feeds` | `/feeds`, `/feeds/create`, `/feeds/edit/{id}`, `/feeds/clone/{id}`, `/feeds/{id}/runs`, `/feeds/{id}/runs/{runId}` | The feed list, the editor, the run history and the live run view. |
| `Wantlist` | `/wantlist` | The wantlist and its sync. |
| `Blocklist` | `/blocklist` | The blocked titles. |
| `Auth` | `/signin`, `/join/{token}`, `/reset/{token}` | Sign-in, joining through an invite link, choosing a new password. |
| `Onboarding` | `/setup`, `/welcome` | The first-run wizard (admin and sign-in method, sink, keys, people) and a new member's prompt for a TMDB key. |
| `Settings` | `/settings` | One section per settings area; Testing only in Debug builds. |

`UI/Shared` holds what the features share: layout and guards, the media grid, detail sheet and global search
(`UI/Shared/Media`), branding and avatars, the signed-in user of a circuit (`UI/Shared/Identity`), the web UI
notification channel, and services such as the external page linkers.

`Web` is the host. Its `Program.cs` adds the modules, then TickerQ (`AddSammlarrScheduling`, from `DB`) and Wolverine
(`UseSammlarrMessaging`, from `Messaging`). Its middleware includes `FirstRunRedirect`, which sends every page to
`/setup` until an admin exists. It maps the Razor components in interactive server mode (with `UI`'s assembly added so
its pages are routed), the auth endpoints under `/auth` (`Web/Auth/AuthEndpoints.cs`), `/health` and `/alive`, and the admin-only
`/diagnostics/export`. `Web/Staging` (the staging seed) is left out of published builds.

## Level 3

### Where a feature lives

A feature area keeps one folder name across `Domain`, `Contracts`, `Core` and `DB/Data`, with the exceptions in the table;
the last column is its UI home:

| Area | `Domain` | `Contracts` | `Core` | `DB/Data` | `UI` |
|---|---|---|---|---|---|
| Feeds | `Feed` | `Feed` | `Feed` | `Feed` | `Features/Feeds` |
| Wantlist and sinks | `Wantlist` | `Wantlist` | `Wantlist` | `Wantlist` | `Features/Wantlist` |
| Titles: loading, ids, details | `Media` | `Media` | `Media` | `Media` | `Shared/Media` |
| Background jobs | `Jobs` | `Jobs` | `Jobs` | `Jobs` | `Features/Home` |
| Sign-in and users | `Auth`, `Identity` | `Auth`, `Identity` | `Auth`, `Identity` | `Access`, `Identity` | `Features/Auth`, `Features/Settings` |
| Notifications | `Notifications` | `Notifications` | `Notifications` | `Notifications` | `Features/Settings` |
| Usage | `Usage` | `Usage` | `Usage` | `Usage` | `Features/Settings` |
| Housekeeping | | `Housekeeping` | `Housekeeping` | `Housekeeping` | `Features/Settings` |

Notifications also live in `Messaging` and `API`, usage in `Observability`. The flows that cross these folders are in the
[Runtime View](04-runtime.md).

What a provider folder holds and how its module registers: [Cross-cutting Concepts](06-crosscutting.md#providers-registration-and-extension).

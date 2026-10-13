# 6. Cross-cutting Concepts

This chapter holds the rules every layer follows, from typed absence to telemetry. The idea to take away: Core never
waits on a service itself. Every remote call passes a lane that paces it by the limits the provider publishes, and Core
decides whom to ask from what the lanes report. The flows these rules serve are in [Runtime View](04-runtime.md).

## Domain model and kernel

`Domain` holds the media model ([ADR 0009](../../adr/0009-model-a-title-as-an-identity-plus-optional-details.md)). A
resolved title is a `MediaMatchResult` union: `MatchedMedia` (a `MediaIdentity` plus details) or `UnmatchedMedia` (its
external ids, details and an `UnmatchedReason`). Details are one `MediaDetails` record (`MovieDetails`, `ShowDetails`,
`UnknownDetails`) with all fields optional. A search provider or enricher answers with `FoundMedia`, which has no
`MediaId`. `Shared` is the kernel: `Optional<T>`, `Result<T>`, `ValueSet<T>` and the `union` types. Unions are matched
by case, never through `.Value`; enum switches are exhaustive with no default arm (CS8509 is an error).

## Absence and failure: `Optional`, `Result` or `null`

A value that can be missing is an `Optional<T>` wherever our own code hands it on, and an expected failure a
`Result<T>`. `T?` stays where the table puts it, and in media details from the provider mapper to the column
(missing genres are an empty set, missing ratings `MediaRatings.None`).

| `Optional<T>` | `T?` |
|---|---|
| non-private returns, parameters and properties of our types: `Find…`, lookups, query criteria, record fields that can be missing (`RunQuery.MustInclude`, `FeedOverview.LastRun`, `NotificationTemplate.Level`), `IMediaSink.AddressedBy` | framework edges: EF rows and LINQ query parameters, JSON-persisted columns (`QueryFilter`'s `FilterPredicate`), provider JSON DTOs, `IOptions` records, Blazor route, query and cascading parameters, `@bind` pairs, Fluid, webhook and Wolverine payloads, Serilog and OpenTelemetry models, BCL overrides and exception constructors |
| a UI parameter or view model whose value comes from the domain | UI-only state (selection, loading, busy, binding, create or edit mode, links), private fields, locals, sort keys |

- `None` means absent, never "not loaded yet"; a failure with a reason is a `Result`.
- An `Optional<T>` member is initialised with `new None()`. `default`, a `null` passed for an `Optional` and an
  unconfigured mock's return hold neither case: `Match`, `Map` and `GetValueOr` throw, and `IsSome`, `IsNone`, `Apply`
  and `ApplyNone` quietly see neither case.
- Across System.Text.Json it needs `OptionalJsonConverterFactory` (Wolverine's serializer registers it; any other must
  add it); an untyped hand-over (`RenderTreeBuilder.AddAttribute`) needs `Optional.Of(x)`. It is never a sort key or
  inside an operator EF translates. `GetValueOr(value)` is eager: pass a `Func` for a costly fallback.
- Side effects go in `Apply`, `ApplyNone` and `ApplyError`, one step each (a step that needs more than one statement
  is a named method); a `ValueTask` step is `async x => await StepAsync(x)`, or it binds the `Action` overload unawaited.
- A `bool` read (`IsSome`, `Match(p, () => false)`) answers a question; outside a loop guard it never routes control
  flow, and a service returns its `Optional`, not a `bool` (`IJobQueue.CancelAsync`).

| To | `Optional`: write | not |
|---|---|---|
| build from a nullable | `Optional.Of(RunIdParam).Map(id => RunId.From(UrlGuid.Decode(id)))` | `RunIdParam is null ? new None() : Some.Of(…)` |
| build from a dictionary | `_byId.GetValueOrNone(id)` | `TryGetValue(…) ? Some.Of(v) : new None()` |
| build from a sequence | `Widths.FirstOrNone(width => width >= size.Width)` | `FirstOrDefault(…) is var width and > 0 ? Some.Of(width) : new None()` |
| build from a condition | `Optional.When(ticks != 0, () => new DateTimeOffset(ticks, TimeSpan.Zero))` | `ticks == 0 ? new None() : Some.Of(…)`, `Optional.Of(cond ? x : null)` |
| keep it only while a condition holds | `next.When(feed.Enabled)` | `feed.Enabled ? next : new None()` |
| project either way | `LinkedRun.Match(linked => …, () => …)` | `LinkedRun is Some<RunId>(var linked) ? … : …` |
| a value or a fallback | `due.Map(Until).GetValueOr(Timeout.InfiniteTimeSpan)` | `due is Some<DateTimeOffset>(var at) ? Until(at) : …` |
| test presence | `_item.IsSome()` | `_item is Some<WantlistItem>` |
| a yes/no that ends the expression | `job.Match(state => state is JobState.Running, () => false)` | `job is Some<JobState>(JobState.Running)`, `job.When(…).IsSome()` |
| route around the value | `query.Feed.When(feed => feed == Feed).Map(…).GetValueOr([])` | `if (query.Feed is not Some<FeedId>(var feed) \|\| feed != Feed) { return []; }` |
| keep the present values | `tconsts.Select(tconst => NumberOf(tconst).Map(id => (tconst, id))).WhereSome()` | `.Where(e => e.id is not null)`, then `e.id!.Value` |
| act when present or absent | `row.Job.Map(job => JobQueue.CancelAsync(job, token)).Apply(_ => …).ApplyNone(() => …)` | `if (x is Some<T>(var v)) { … }`, `if (x.IsSome()) … else …`, `if (x.Map(…).GetValueOr(false))`, one block lambda |
| hand it to a framework | `query.MustInclude.Map(run => run.Value).ToNullable()` | `is Some<RunId>(var id) ? id.Value : null` |
| turn it into a `Result` | `Optional.Of(answer?.Data).ToResult("AniList answered without data.")` | `is not Some<…> … { return Error.From(…); }` |

| To | `Result`: write | not |
|---|---|---|
| project either way | `await setupCode.Match(…, () => signIn.CompleteAsync(…)).Map(user => SignInAndRedirectAsync(context, user, returnUrl)).GetValueOr(error => Results.Redirect(…))` | `switch { Success<User>(var user) => …, Error error => … }` |
| pick a case, both sync | `PlanAsync(sink).Match(summary => (WantlistPlanResult)summary, error => new WantlistSyncSinkUnreachable(error.Reason, …))` | a type `switch` over the cases |
| act on success or failure | `result.Apply(…).ApplyError(…)` | `if (result is Success<T>(var v)) { … } else { … }` |
| keep the successes of a stream | `await foreach (var media in stream.WhereSuccessAwait())` | `if (result is Success<T>(var v))` inside the loop |
| keep a success that passes a test | `answer.ToOptional().When(found => found.Fits(type))` | `is Success<T>(var found) && …` |
| read the cause of an error | `refused.CausedBy<RemoteUnavailableException>().Match(unavailable => NotReached(number, unavailable), () => refused)` | `Error { Exception: RemoteUnavailableException }` |
| chain a fallible step | `.Map(next)` (flattens a `Result`-returning step), `.MapError(…)` | a guard that returns the error by hand |
| leave the monad at a boundary | `.GetOrThrow(error => new …Exception(error.Reason))`, `.ToOptional()` | `is Error e ? throw … : …` |

Outside `Shared`, a monad is matched by hand only in a guard (`if (x is not Some<T>(var v)) { return …; }`) that
leaves a loop, or whose following code holds a method-scoped `using var` or a `try`; Razor markup may `@if` on a case.
The build enforces this: `SAM0002` (`Roslyn/Monads/MonadPatternAnalyzer.cs`) fails hand matches, null and shape
patterns, `default` or `null` as a monad, `?:` constructions, block-lambda or unawaited steps, and a monad used as a
sort key or as text; `SAM0003` (`MonadBranchAnalyzer.cs`) fails a branch that reads a monad as a `bool`; the
`AbsenceTests` architecture test fails a nullable non-private member outside the edges it lists.

## Modules and dependency injection

Every layer and provider ships an `IServiceModule` (`Shared/DI`), composed in `Web/Program.cs` with `AddModule<T>()`
([Building Block View](03-building-blocks.md)). A service with several implementations is keyed by an enum
(`IFeedProvider` by `FeedProvider`), resolved through `IKeyedServiceIndex<TKey, TService>` or, to list the keys and
find one, `IKeyedServiceSet<TKey, TService>`.

## Contract shape

- **Snapshots for small lists.** The wantlist, blocklist, outbox and feeds are small per user, so one read returns the
  whole list as a Domain snapshot (`WantlistSnapshot`, `BlocklistSnapshot`, `FeedSnapshot`, `OutboxSnapshot`).
- **Repositories access, services compose.** A repository returns items and counts from its own tables; the service
  builds the snapshot. UI injects no repository but the notification settings ones, nor `IWantlistOutbox`.
  `RepositoryTests` pins who may use each repository and keeps each aggregate's persistence off the others' tables.
- **A query record for unbounded history.** Feed runs are paged through `GetRunsAsync(RunQuery)`.
- **Verbs.** A member's verb says what it does and its type what it acts on: `Get` reads what exists, `Find` what may
  be missing (an `Optional`), `Count…By` returns a dictionary.

## Providers: registration and extension

Providers are registered by hand ([ADR 0010](../../adr/0010-split-the-ids-from-the-services-sammlarr-calls.md),
[ADR 0011](../../adr/0011-give-a-service-every-role-its-api-supports.md)): a provider module registers its clients,
key check, roles, id converters and enrichers through `ForRemote<TOptions>(RemoteService)` (`API/Http`), Radarr +
Sonarr their sink and search through `ForService`. Each role registration records who registered it and which service
the role claims (`Registered`).

- **A service:** a `RemoteService` member with its `Access` and `OwnIds` arms, its brand
  (`UI/Shared/Services/Branding/MediaProviderBrandingService.cs` plus the art), an options record with its section
  (and lanes) in `Web/appsettings.json`, and a module under `API/<Service>`. A key adds an `ApiKeyProvider` slot and a
  key check registered with `ApiKey<T>(slot)`; a sign-in adds an `AuthProvider` member and one class, registered with
  `Credential<T>`, that signs people in and checks their stored token; a keyless service is `Open`.
- **An id source:** a `MediaProvider` member with its id type, its arms in `MediaProvider.Role` and the stored-id
  conversion (`DB/Data/Media/MediaIdConversionExtensions.cs`), its brand and an `IExternalPageLinker`.
- **A rating source:** a `RatingSource` member with `[SuppliedBy<RemoteService>]` (a generator derives `SuppliedBy()`
  and `SuppliedRatingSources()`), a rating type behind `IOwnedRating<TSelf>`, its `Card` arm, each supplier's wire
  mapping and its `RatingTexts`.
- **A downloaded file:** an `IDataset<TRow>`, an `IDatasetTable<TRow>` and a `DatasetLoader<TRow>` ([Datasets](#datasets)).
- **A feed source:** a `FeedProvider` member with its `Service`, a `Feed<T>` registration and a UI `FeedSources` entry;
  browse pages go through a `SiteLink` page table (`AniListPages`, `MalPages`, `TmdbPages`, `TraktPages`). A feed
  source throws on a shape it cannot read and never skips an item.

A member that misses one of these fails the build (the exhaustive switches) or a test: `RoleRegistrationTests` (each
registration matches its claimed service; every role member, remote service and rating source is covered),
`MediaProviderBrandingServiceTests` (brand art), or `StoredEnumNamesTests` (every stored enum name).

## Lanes and HTTP resilience

Every call to a remote service passes a _lane_ (`LaneGate`, `Shared/Concurrency`), one per service, route and owner,
named and routed by `Lanes` (`API/Http/Lanes`). Lanes pace each service by its published limits, so Core never sleeps
or throttles ([ADR 0017](../../adr/0017-pace-remote-calls-on-lanes-with-limits-as-published.md)). A client registered
through `ForRemote(...).Client` gets this pipeline from `AddRemoteHttp<TOptions>`, outermost first:

1. `UsageStampHandler` stamps the request with its service and cause.
1. A Polly retry (`LanedPipeline`), the only retry layer: up to `Http.RetryAttempts` retries of a 5xx, 408, 429 or
   broken connection that the lane would admit now (`Lanes.Admits`), with no delay of its own after a 429.
1. `LaneHandler` takes one lane permit per attempt, so a retry queues like any request.
1. The attempt timeout (`Http.AttemptTimeoutSeconds`), whose expiry is not retried.
1. `UsageTagsHandler` tags each attempt.

Clients that call no remote service (Pushover, the webhook, the picture grids, GitHub's release feed) get
`AddClientHttp<TOptions>`'s standard resilience handler, with a total timeout of `Http.TimeoutSeconds`.

A request takes the lane whose route (a key of `IProviderHttpOptions.Urls` plus a path) is the longest prefix of its
address, else the `api` lane (`LaneSettingsCheck` fails start-up on a route naming a URL key the provider lacks).
MDBList makes its API key the lane owner, Radarr and Sonarr their address, so each key and app has lanes of its own.

### Limits per service

A limit is written as the provider publishes it, requests per window (`RateWindow`), never flattened to a steady
per-second rate. A lane may also cap requests in flight (`MaxInFlight`) and batch ids (`MaxBatch`). This is the docs'
one table of each provider's `Http` section in `Web/appsettings.json` (records `ProviderHttpOptions`, `LaneOptions`):

| Service | Lanes (`Http.Lanes`) | Waits |
|---|---|---|
| TMDB | `api` 40 per second | `MaxWaitSeconds` 15, `EnrichWaitSeconds` 15 |
| Plex | `api` (plex.tv and the rest) and `metadata`: 50 per second, 25 in flight each; `match`: 300 per minute, 5 in flight; `watchlist`: 4 in flight (Plex publishes no limit) | `MaxWaitSeconds` 90 |
| MDBList | `api` 1,000 per 5 minutes on aligned windows (:00, :05, …); `titles` batches of 200 | |
| Trakt | `api` 1,000 per 5 minutes | |
| AniList | `api` 30 per minute, 1 in flight; `titles` and `prequels` batches of 50 | |
| MyAnimeList | `api` 30 per second, 1 in flight | |
| TVmaze | `api` 20 per 10 seconds | |
| Wikidata | `api` 5 in flight; `sparql` batches of 100 | |
| Radarr, Sonarr | `api` 4 in flight | |
| IMDb datasets, anime lists | `api` 2 in flight | |

Every lane keeps the `LaneOptions` defaults for going down: `FailuresToDown` 4, `DownSeconds` 30. A wait left blank is
0: the lane refuses while the service is blocked, and its enricher is asked only while it is open.

### Admission and refusal

A lane admits waiting attempts by `LaneClass`, then by order within the class:

| `LaneClass` | Set by | Order inside |
|---|---|---|
| `Interactive` | anything unset: a search, a click | arrival |
| `Source` | feed list pages (`SourcePages.ReadAsync`) | page order |
| `Title` | each title's chain in a list load | list order |
| `Background` | the details fill, identity confirmation | oldest first |

- A 429 with `Retry-After` blocks the lane until then; one without halves the rate, at most once per window (a lane
  with no window pauses for a second), and each quiet window gives back a tenth of its requests, at least one.
- `FailuresToDown` failed attempts in a row (5xx, 408, timeout, broken connection) take the lane down for
  `DownSeconds`; then one probe decides.
- Waiting for room in a window is never refused. A down lane, or a block or quota reserve that outlasts the service's
  `MaxWait`, refuses at once with `LaneRefusedException`, before anything is sent.
- A quota is the lane's `ILaneQuota`, asked per class. MDBList's (`MdblistBudget`) always admits list pages, stops
  title lookups and `Interactive` asks once only `MDBList:FeedReserve` requests are left before the reset, and stops
  `Background` work once less than `MDBList:BackgroundReserveShare` (half) of the day's allowance is left.

A service that takes many ids per request also gets a keyed lane (`Lane<TKey, TOut>`): MDBList's title lookups,
AniList's titles and prequels, Wikidata's SPARQL lookups. One batch per batch key and class is out at a time; what
arrives meanwhile rides the next, up to `MaxBatch`. The batch's request takes a permit and the retry like any other;
the keyed lane never sends a batch again, and a failed batch fails every key it carried.

`Answering.GuardedAsync` (`API/Http`) turns a converter's or enricher's outcome into an `Answer<T>` (MDBList's batched
answers go through `LaneAnswers.ToAnswer`, which maps exceptions the same way): `Found`; `NoMatch` for an error without
an exception (a 404); `Unavailable` for a lane refusal or rejected key (not sent), an HTTP error or timeout (sent), or
a `RemoteUnavailableException` (a 429, 408 or 5xx past the retries, a dataset not loaded yet), which says itself
whether it was sent (`Sent`); `Faulted` for anything else. A cancellation is rethrown. Only `NoMatch` from every route
settles a title as not found.

## Provider routing

Core asks a connected service only when its block ends within the right budget; the lane does any waiting.
`IRemoteCapacity.Of(remote)` folds a service's lanes into a `RemoteLoad` (`BlockedUntil`, `EstimatedWait`, `Down`,
`Paced`, the budgets), read for the asking class (so the details fill sees MDBList blocked at its floor while a feed run
sees it open); an owner's lanes count only for the user who sent through them. `IServiceReadiness` decides what is
connected from `RemoteService.Access` (always for a keyless service; a stored key or sign-in not rejected since it was
stored; a Radarr or Sonarr address and key), never by calling the service. A key check answers `Valid`, `Rejected`,
`Unreachable` or `Spent`; only `Rejected` is remembered.

### Wait budgets

`RemoteLoad.Takes` holds while the service is not down and its block ends within the budget; `AnswersWithin` also
needs the estimated wait inside it. Values are in the [lane table](#limits-per-service).

| Budget | Setting | Gates |
|---|---|---|
| `MaxWait` | the provider section's `Http:MaxWaitSeconds` | each hop of an id lookup path; the lane's patience before refusing (0 by default: only while open) |
| `EnrichWait` | the provider section's `Http:EnrichWaitSeconds` | every enricher ask (0: only while open) |
| `ShownWait` | `Sammlarr:FeedLoad:ShownWait` | an ask for a field the load shows but does not filter or sort on (every field for a stale or uncached title and for the details fill): the service must also answer within it |

### Enricher choice

`DetailsFetcher` asks enrichers one at a time while a needed field is missing (`EnricherChoice.Next`). Candidates are
enrichers of connected services that accept one of the title's ids (`Accepts`), are not yet asked or resting, and fill
a missing field within the budgets above. It picks open services first, then the search setting's service while a
detail field is missing, then the most missing detail fields, then the most missing rating sources, then the least
estimated wait, then `RemoteService` order, and stops when nothing needed is missing or no candidate is left.

- An enricher that answered rests for that title for `Sammlarr:Housekeeping:FreshDays`; one that failed after sending
  rests 1, 6, then 24 hours; one skipped or refused records nothing.
- A fresh answer wins over the stored copy, an earlier answer over a later one. What an enricher `Defers` (TVmaze,
  AniList and MyAnimeList their titles) and artwork no `IArtworkResizer` can size only fill gaps left after the rest.
- A `RatingSource` without a rating counts as a missing field, as does a vote-less rating from a source that publishes
  votes (all but Rotten Tomatoes and Metacritic). One rating is kept per source; one with votes wins.
- A stale refresh that skipped a service keeps the stored values of that service's fields no answer filled and, if it
  kept any, `RefreshedAt`, so the title is served as `Stale` and retried on the next load.

### Id lookup paths

`MediaIdResolver` finds a title's id at a target provider from the ids it holds. Each `IExternalIdConverter` announces
`ById` lookups with what they accept, yield and cost in calls (`ByHints` is supported, but no converter announces one).

| Service | Lookups: from → gives | Cost |
|---|---|---|
| Anime lists | AniList, MyAnimeList → those, TheTVDB, TMDB, IMDb; TheTVDB, TMDB, IMDb → AniList, MyAnimeList | 0 |
| Plex | match (`AsksOnce`): TMDB, IMDb, TheTVDB → Plex; metadata: Plex → TMDB, IMDb, TheTVDB | 1 |
| TMDB | detail: TMDB → IMDb, TheTVDB; find: IMDb, TheTVDB → TMDB | 1 |
| Trakt | Trakt, IMDb, TMDB, TheTVDB → the same four | 1 |
| TVmaze | IMDb, TheTVDB → TVmaze, TheTVDB, IMDb | 2 |
| MDBList | IMDb, TMDB, TheTVDB, Trakt, MyAnimeList → the same and MDBList | 2 |
| Wikidata | IMDb → TMDB, TheTVDB, AniList, MyAnimeList, rating site pages | 3 |

`IdLookupPlanner` plans every path of up to three hops from the held ids to the target, ordered by weight (each hop's
cost times one plus its service's estimated wait in seconds, `RemoteLoad.Weight`), then by length.

- Free lookups are asked once before planning, adding ids only for sources the title lacks; no path runs through one.
- A lookup is asked once per input per resolution; an `AsksOnce` lookup's definitive answer covers the other ids.
- A hop already answered no for every input ends its path as a no-match. Otherwise a service ahead that is down,
  blocked past its `MaxWait`, or unavailable earlier makes the path give up; one blocked within `MaxWait` is asked.
- A title is cached as unmatched (in memory, for an hour) only when no path gave up.

## Identity keys, page links and claims

A title's identity is its `MediaId` plus its identity keys
([ADR 0018](../../adr/0018-claim-identity-before-hand-out-from-identity-keys-only.md)). The exhaustive
`MediaProvider.Role` switch (`Domain/Providers/ExternalIdRole.cs`) sorts every external id into one of two kinds:

- An _identity key_ (`IIdentityKey`: TMDB, IMDb, TheTVDB, Plex, MDBList, Trakt, MyAnimeList, TVmaze, AniList) names one
  title; matching and claims use keys only. Keys live in `ExternalMediaIds`, keyed by source, media type and value,
  with at most one key per source on a title.
- A _page link_ (`IPageLink`: Rotten Tomatoes, Metacritic, Letterboxd) hangs off the details (`MediaDetails.PageLinks`),
  never the identity: remakes share a Metacritic slug, so a link never merges titles.

Every path that identifies a title claims it before handing it out, through the claim lane (`IIdentityClaims`;
walkthrough in [Runtime View](04-runtime.md)):

- An `IdentityClaim` carries its keys, the title it is pinned to (if any) and the services that produced them. The lane
  commits every queued claim in one transaction under `MediaWriteLock`; a constraint error conflicts that claim only.
- `ClaimRule.Decide` is pure (`ClaimRuleTests`, `ClaimRuleLaws`). A claim extends at most one stored title, never adds
  a second key of one source or a key of another media type, and when pinned drops what it cannot take. A claim that
  would join two stored titles conflicts: the title settles unmatched and both keep their keys. A key another title
  holds notes the pair as stored twice (the `IdentityConflicts` table, read through `IIdentityConflicts`).
- A search hit is the exception: it shows as the stored title that holds the search provider's own key (TMDB's,
  Plex's, or for Radarr + Sonarr TMDB's for a movie and TheTVDB's for a show), and stays unmatched, so not addable,
  only when none does (`MediaSearchService`).

Two stored titles become one only through `IMediaMerge` (`DB/Data/Media/MediaMerge.cs`), called by `DuplicateTitles`
and an admin's review, in one transaction under `MediaWriteLock`. It refuses a pair with ids of one source on both or
two media types (`IdentityConflict.Contradicts`). The survivor (the title a wantlist or blocklist entry points at, else
the older one) takes every id, fills its details field by field, takes the other's ratings, page links and answers for
sources it lacks, and counts as refreshed at the earlier `RefreshedAt`. Rows move set-based across users
(`IgnoreQueryFilters`); where one user holds both, the older entry, the outbox action (per sink) that agrees with their
wantlist, and one run item per run stay. Then the other title is deleted.

## Persistence and housekeeping

State is one SQLite file through EF Core ([ADR 0004](../../adr/0004-store-all-state-in-one-sqlite-file.md),
[ADR 0003](../../adr/0003-use-ef-core-migrations-for-the-schema.md)).

- Repositories create a context per operation from `IDbContextFactory<SammlarrDbContext>` (the context is `internal`).
- `SqlitePragmaInterceptor` sets WAL and a 5-second busy timeout. Wolverine expires its own dead letters, and
  housekeeping leaves its tables alone.
- `IDatabaseFile.CompactAsync` truncates the log and vacuums when a tenth of the pages are free, after a dataset load
  and after the housekeeping pass.
- `DbInitializer` applies the migrations in `DB/Migrations` at start-up; `UseSeeding` inserts the provider-config row.
  Enums are stored as names, `DateTimeOffset` as binary; `SaveChanges` stamps audit timestamps and the owner.

The `Housekeeping` ticker runs `HousekeepingPass` nightly at 03:00 (`HousekeepingSchedule`), or on demand from
Settings › Housekeeping. It takes the job runner's turn (`JobTurn`), runs each step in order, saves its report after each
step and compacts the file; a failing step stops the pass. The windows are
`Sammlarr:Housekeeping` settings, except the fixed 30 days:

| Step | Removes |
|---|---|
| `RunHistory` | a run's titles, calls and answer times after `RunHistoryDays` (the run keeps its counts) |
| `UsageStats` | usage rows and deleted feeds' names after `UsageDays` |
| `CachedDetails` | details not refreshed for `FreshDays + DetailsUnusedDays` |
| `TitleIds` | a title and its ids once it has had no details for `IdsUnusedDays - DetailsUnusedDays` (from `Media.DetailsTrimmedAt`), or `IdsUnusedDays` after it was added if it never had any |
| `DuplicateTitles` | settles due stored-twice notes |
| `SettledJobs`, `ScheduleEntries` | finished jobs, TickerQ occurrences and one-off runs after 30 days |

- A title on a wantlist, a blocklist, the outbox or in a kept run (`ProtectedMediaIds`) keeps its details and ids. The
  details and ids trims hold `MediaWriteLock`.
- `DuplicateTitles` keeps a pair whose rows contradict apart and asks about every other pair through
  `IIdentityConfirmation`: each user in turn, at the `Background` class, asks the lowest-weight connected converter
  outside the claim's services (`IdentityConflict.Sources`) for the claimed id's source; one that refuses without
  sending passes the note on, to the next converter, then the next user. The claimed id merges the pair, another keeps
  it apart, and no answer asks again after 1, 7, then 30 days.

## Secrets and configuration

- `[Encrypted]` (`DB`) encrypts stored secrets (credentials, API keys, the Radarr and Sonarr keys, notification-target
  secrets) through an EF converter over ASP.NET Core Data Protection. `[Sensitive]` (`Contracts`) only masks a member
  as `***` in logs; a Domain type names its secrets in `SensitiveModel` (`Observability`) instead.
- Static settings (URLs, timeouts, lanes) are options records in `Shared/Options`, each an `IOptionsSection` naming
  its section; a generator (`Roslyn/Options`) binds them all, so a component injects the record itself.
- Settings an admin changes at run time (the sink, the Radarr and Sonarr connections with their keys, the trending,
  search and sign-in providers, the registration mode) are `ProviderConfig`: one global row, cached by
  `ProviderConfigService`.

> [!NOTE]
> Data-protection keys live in a key ring under the application name `Sammlarr` (`EncryptionModule`), so stored
> secrets survive a restart. The ring is `dp-keys` beside the database file unless the environment variable
> `Sammlarr__DataProtection__KeyRingPath` names another folder; configuration files cannot set it. In the container
> the ring shares the `/data` volume ([Deployment View](05-deployment.md)).

## Authentication and per-user isolation

- An install signs in one way, `ProviderConfig.AuthProvider`: `Plex`, or `Local` accounts whose password is hashed by
  ASP.NET Core's `PasswordHasher` and posted as a form to `/auth/local` (a Blazor circuit cannot set a cookie).
  Password sign-ins are rate-limited per address and checked one at a time per account; five wrong passwords lock the
  account for 15 minutes. The Plex round trip binds its state to the browser with a short-lived cookie.
- The auth cookie is checked against the stored user and role on every request, so a removed user or a changed role
  is signed out at once. A password change sets `SessionsRevokedAt`, which rejects older cookies; a password change,
  role change or removal signs open circuits out (`ISessionInvalidator`).
- Isolation lives in persistence ([ADR 0005](../../adr/0005-enforce-per-user-data-isolation.md)): an `IUserOwned`
  entity gets a global query filter on a shadow `UserId`, the current-user lookup throws when nobody is signed in, and
  an ownership stamper sets the owner on insert and blocks changing it. Feeds, wantlist, outbox, blocklist,
  notification targets, API keys and sign-ins are per user; `ProviderConfig` (Radarr and Sonarr included) and the media
  catalogue are global. Background code (the job runner, the feed ticker, Wolverine handlers) runs as the owner through
  `IUserScope.AsAsync(owner)`. Isolation tests: [Quality Requirements](07-quality.md).

## The job queue

The rules every job follows; the walkthrough of a run and a sync is in [Runtime View](04-runtime.md).

- Feed runs, wantlist syncs and the details fill go through one queue of record, the `Jobs` table (`IJobQueue`), and
  one hosted `JobRunner` that runs one row at a time in a fresh DI scope as its owner
  ([ADR 0006](../../adr/0006-run-background-work-through-one-job-queue.md)). The runner holds `JobTurn` meanwhile; the
  housekeeping pass and an admin's merges take the same turn.
- A feed's TickerQ cron ticker only enqueues. Enqueueing coalesces by kind, key and owner, and a deferred row waits in
  `Waiting` until `JobAlarm` releases it (delays under `Sammlarr:Scheduler`).
- A job whose handler throws ends `Failed`, with no automatic retry. The owner can cancel a `Waiting` or `Queued` job
  from Up Next on Home; one the runner already took runs. At start-up a `Running` row goes back to `Queued`, or ends
  `Cancelled` when a like row is already pending.
- The details fill ([FillDetails](04-runtime.md#filldetails)) steps aside: it waits
  `Sammlarr:Scheduler:FillDetailsGrace`, then runs only while no feed run or sync is queued or waiting, or once
  `Sammlarr:Scheduler:FillDetailsMaxWait` has passed.
- A push to the active sink is a step of the sync: pending changes wait in the wantlist outbox and the sync drains it
  ([ADR 0007](../../adr/0007-push-watchlist-changes-inline-during-sync.md)).

## Datasets

A downloaded dataset (IMDb's ratings file, Fribb's anime id map) belongs to the whole app, so it stays out of the
per-user queue ([ADR 0013](../../adr/0013-load-shared-datasets-outside-the-job-queue.md)). `DatasetSchedule` registers
a daily TickerQ ticker per `IDatasetLoader` at 04:30, plus a one-off ticker before the first load. The loader asks
with the last `ETag`, replaces the table in one transaction (a failed or empty file keeps the old one), and records
the load in `DatasetLoads` and the outcome in `DatasetChecks` (Settings › Housekeeping). Until the first load, its
enricher and converter answer `Unavailable`.

## Events and notifications

Anything that reports "something happened" is one of two disjoint kinds, chosen only by whether a user could
reasonably subscribe to it ([ADR 0008](../../adr/0008-separate-events-from-notifications.md)):

| | Event (`IEvent`) | Notification (`INotification`) |
|---|---|---|
| Examples | push status per item, job state, a feed just ran, wantlist and blocklist changes | a sync or feed run finished or failed, a provider is rate limiting |
| Transport | in-process `IEventPublisher` to open pages; never stored | Wolverine, persisted in the app database, to each matching subscription's channel (webhook, Pushover, in-app toast) |
| Retries | none | after 1, 5 and 15 minutes and 1 hour; 5 attempts at most |

Core publishes through `Announcer` (`Announce(IEvent)`, `AnnounceAsync(INotification)`); only the lanes' rate-limit
report (`ProviderRateLimitReporter`) uses `IMessagePublisher` directly.

## Logging and observability

- Serilog with two destructuring policies: one logs a union's payload instead of the wrapper, the other masks
  `[Sensitive]` and `SensitiveModel` members as `***`.
- Sinks: the console; OTLP for logs, traces and the usage meters when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (Aspire
  sets it); an in-memory capture, started in Settings › Diagnostics, that keeps logs and `Sammlarr` spans for the
  admin-only `/diagnostics/export`.
- Tracing is OpenTelemetry over the `Sammlarr` and `Sammlarr.FeedRun` sources plus ASP.NET Core and HttpClient. A
  service opts in through `AddTracedScoped` or `AddTracedKeyedScoped`, a proxy that spans each call. Without OTLP,
  `ActivityLogProcessor` logs spans that errored.

## Usage metrics and Settings › Usage

Every call to a service is counted with the built-in instruments, the retry pipeline per call and HttpClient per
attempt ([ADR 0014](../../adr/0014-count-provider-calls-with-the-built-in-instruments.md)).

- Handlers stamp each call with its `RemoteService`, cause and outcome (`Answered`, `AnsweredAfter429`, `GaveUpOn429`,
  `Failed`, `Cancelled`). A call that never got a lane permit is not counted.
- The cause is an ambient mark (`UsageCauses.Mark`) set at each entry point; the outermost wins, so titles shown inside
  a feed run count for the run. `UsageCause`: `FeedRun`, `WantlistSync` (the sync dialog, adding from a page, a sink
  switch, removing a person), `Dataset`, `Search`, `Browse`, `ShowingTitles` (including the rating breakdown's id
  lookups), `KeyTest`, `FeedEditor` (previews, the link check, saving, the dry run before Run now), `DetailsFill`,
  `Housekeeping`, `Other`.
- Tags stay bounded (service, cause, outcome, feed id); a new feed's previews carry its draft id, and Settings › Usage
  folds ids of feeds never saved into one row.
- `UsageExporter` folds each minute's deltas into hourly rows (`RemoteUsage`, `RemoteAttempts`). A call marked
  `FeedRun` is also credited to the running run (`IFeedRunUsage`) and saved in `FeedRunCalls`.

Settings › Usage (`UsageReport`, admin-only) reads the hourly rows per service, cause and feed; a deleted feed's name
stays in `DeletedFeeds` until its rows are pruned. For MDBList it adds the day's remaining allowance (`MdblistBudget`,
`QuotaDay.Of`).

## Live run trace

While a feed runs, its owner can watch its titles move through the services (the Feeds page's live view and the card's
running pill). The view only observes: a hook that records a step never awaits, never throws and changes nothing the
run decides ([ADR 0016](../../adr/0016-live-runs-pull-from-an-in-memory-trace.md)).

```mermaid
flowchart LR
  R[ListLoad, TitleIdentifier,<br/>MediaIdResolver, DetailsFetcher,<br/>feed sources] -->|FeedRunSpans| A[Sammlarr.FeedRun<br/>+ System.Net.Http spans]
  A -->|own ActivityListener| T[FeedRunTrace<br/>singleton, in memory]
  A -.->|OTel SDK| O[OTLP, dashboard]
  T -->|IFeedRunTraces.ReadAsync<br/>every 250 ms| W[FeedRunProgress per circuit<br/>FeedRunWatch per run]
  W --> L[FeedRunLive] --> J[feed-live.js<br/>replays at recorded timing]
  W --> C[card running pill]
```

- Spans start through `FeedRunSpans` only under a run span (`IFeedRunTrace.Start`, called by `FeedRunEngine`), so
  Browse, search and the editor preview record nothing. `FeedRunTrace` reads them with its own `ActivityListener`, so
  the view survives sampling. HTTP spans under a hop mark it answered after a 429, or given up.
- The trace keeps one run per feed for 30 seconds after it ends, only for the feed's owner; past 50,000 steps it drops
  path, hop and give-up steps.
- Pages pull rather than subscribe: each circuit's `FeedRunProgress` holds a `FeedRunWatch` per run that reads every
  250 ms, and the browser replays each batch 250 ms behind.
- Each read also brings the run's progress and each service's load, so a blocked lane and one pacing to its window
  (`RemoteLoad.Paced`) show their countdowns.
- The card's last-run bar and every saved number come from the run `FeedRunEngine` records (`FeedRunCounts`), never
  from spans.

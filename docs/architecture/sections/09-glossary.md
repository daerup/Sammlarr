# 9. Glossary

Sammlarr's own terms, each linked to the section that explains it.

## Titles and identity

**Library**: Every stored title, shared by all users: its `MediaId`, identity keys and cached details
([Runtime View](04-runtime.md#loading-a-list)).

**Media provider**: An id namespace (`MediaProvider`), not a service Sammlarr calls
([Context and Scope](02-context-scope.md#ids-without-a-service)).

**Identity key**: An external id that names one title, such as a TMDB or IMDb id. Only keys match titles and take part
in claims ([Cross-cutting Concepts](06-crosscutting.md#identity-keys-and-page-links)).

**Page link**: A title's page on Rotten Tomatoes, Metacritic or Letterboxd; it never identifies a title
([Cross-cutting Concepts](06-crosscutting.md#identity-keys-and-page-links)).

**Show part**: A later season's, part's or special's own AniList or MyAnimeList id, stored with the show it led to
(`IShowParts`), so the next anime list load goes straight to the show without walking the anime id map or asking AniList.
An answer that led to no show is kept for `NoPrequelDays` (3 days)
([Cross-cutting Concepts](06-crosscutting.md#identity-keys-and-page-links)).

**Claim**: The write that fixes a title's identity before Sammlarr hands it out (`IIdentityClaims`). A pinned claim
extends only its title; an unpinned one finds or starts its title; none merges two ([Runtime View](04-runtime.md#ids-and-the-claim)).

**Stored twice**: Two stored titles a claim's keys tie together, noted (`IdentityConflict`) until housekeeping or an
admin merges them or keeps them apart
([Cross-cutting Concepts](06-crosscutting.md#identity-keys-and-page-links)).

## Loading a list

**List load**: Turning a list's entries into library titles, handing each back as it settles
(`IMediaService.LoadListAsync`) ([Runtime View](04-runtime.md#loading-a-list)).

**Burst**: What a list source yields without waiting, such as one page. The load reads the library and primes the
services once per burst ([Runtime View](04-runtime.md#bursts-and-chains)).

**Chain**: One title's work in a list load: its ids, its claim and its details, in list order
([Runtime View](04-runtime.md#bursts-and-chains)).

**Settle**: How a chain ends, exactly once, with one outcome ([Runtime View](04-runtime.md#bursts-and-chains)):

| Outcome | Meaning |
|---|---|
| Matched | A library title holding the sink id. |
| Not found | No service knows the title, or the sink cannot take its type; a lookup miss is cached for an hour. |
| Not found earlier | That cache answered; no service was asked. |
| Couldn't look up | A service it needed was down, blocked too long or faulted, and nothing else answered. Not cached. |
| Conflict | Its keys sit on two stored titles, or contradict each other. |

**Id converter**: A service's id lookups (`IExternalIdConverter`), which `MediaIdResolver` chains into paths toward the
sink id ([Runtime View](04-runtime.md#ids-and-the-claim)).

**Enricher**: A service's details lookup (`IDetailsEnricher`), asked one at a time until nothing the load needs is
missing ([Runtime View](04-runtime.md#details)).

**Fresh, stale**: A stored title is fresh for `FreshDays` (14) after its last refresh; the next load refreshes a stale
one ([Runtime View](04-runtime.md#details)).

**Resting**: An enricher that answered for a title is not asked again for `FreshDays`; one that failed waits 1, 6,
then 24 hours ([Runtime View](04-runtime.md#details)).

**Dataset**: A file downloaded daily into a table (IMDb's ratings, the anime id map); its lookups cost nothing
([Cross-cutting Concepts](06-crosscutting.md#datasets)).

## Services

**Remote service**: One media service Sammlarr calls (`RemoteService`); its `Access` says what a person needs to use
it ([Context and Scope](02-context-scope.md#external-partners)).

**Connected**: A remote service whose access is in place for the current user; only connected services are asked
([Cross-cutting Concepts](06-crosscutting.md#provider-routing)).

**Lane**: The gate every call to a remote service passes (`LaneGate`). It paces calls to the provider's published
limits and refuses, unsent, when down or blocked too long
([Cross-cutting Concepts](06-crosscutting.md#lanes-and-http-resilience)).

**Wait budget**: How long a load waits on a blocked service: `MaxWait` per id lookup, `EnrichWait` per enricher ask,
`ShownWait` for fields a card only shows ([Cross-cutting Concepts](06-crosscutting.md#wait-budgets)).

## Feeds and ratings

**Feed**: A saved list or chart from MDBList, TMDB, Trakt, AniList or MyAnimeList, with a strategy, optional
filter, sort and limit, and a schedule or none ([Introduction and Goals](01-introduction-goals.md#requirements-overview)).

**Append, Mirror**: A feed's strategy (`FeedStrategy`). `Append` only adds; `Mirror` also removes the titles it added
once they miss `AbsenceThreshold` conclusive runs in a row ([Runtime View](04-runtime.md#the-run)).

**Conclusive**: A load that finished, admitted a title and settled none couldn't look up; only then may Mirror remove
([Runtime View](04-runtime.md#the-run)).

**Live run view**: The Feeds page's view of a running feed's titles moving through the services, read from an
in-memory trace ([Cross-cutting Concepts](06-crosscutting.md#live-run-trace)).

**Rating source**: One of nine places a rating comes from (`RatingSource`), such as IMDb; it counts while a connected
service supplies it ([Cross-cutting Concepts](06-crosscutting.md#providers-registration-and-extension)).

**Sammlarr rating**: The 0 to 10 rating Sammlarr shows, filters and sorts by: the first available source in a chosen
order, or the average of chosen sources ([Introduction and Goals](01-introduction-goals.md#requirements-overview)).

## Wantlist and sinks

**Wantlist, blocklist**: The titles a person wants, synced with the sink, and those no feed may add
([Runtime View](04-runtime.md#wantlist-sync)).

**Provenance**: Where a wantlist title came from: the sink, by hand, a feed (which then owns it), or a deleted feed
(orphaned) ([Introduction and Goals](01-introduction-goals.md#requirements-overview)).

**Sink**: Where the wantlist syncs (`SinkKind`): each person's Plex watchlist, or the install's Radarr and Sonarr as one
target ([Context and Scope](02-context-scope.md#sign-in-and-sinks)).

**Sink id**: The id the sink adds a title by: its Plex id on Plex; on Radarr + Sonarr its TMDB id for a movie, its
TheTVDB id for a show ([Runtime View](04-runtime.md#ids-and-the-claim)).

**Unaddressable**: A title the sink cannot add: no sink id, or unknown to Radarr or Sonarr. Recorded instead of failing
([Runtime View](04-runtime.md#wantlist-sync)).

**Outbox**: Wantlist changes not yet pushed, one row per user, title and sink; opposing changes cancel
([Runtime View](04-runtime.md#wantlist-sync)).

**Sync**: A `SyncWantlist` job: pull what changed in the sink, then push the outbox
([Runtime View](04-runtime.md#wantlist-sync)).

**Tag scheme**: How Radarr and Sonarr titles are marked as Sammlarr's (`ArrTagScheme`): user, feed and plain tags under
one prefix, and `protected` on titles the app already had
([Runtime View](04-runtime.md#radarr--sonarr-tags-mark-whose-a-title-is)).

## Jobs and notifications

**Job**: A row in the `Jobs` table (`RunFeed`, `SyncWantlist`, `FillDetails`) that `JobRunner` runs one at a time as
its owner; a repeat folds into the pending one ([Runtime View](04-runtime.md#feed-runs-and-the-job-queue)).

**Grace, MaxWait**: A scheduled run, or a manual one with _sync when done_, arms a sync due `Grace` later; each further
such run pushes it back, never past `MaxWait` after its first due time. Not the wait budget
([Runtime View](04-runtime.md#feed-runs-and-the-job-queue)).

**FillDetails**: The job that fills missing fields of a person's wantlisted and blocklisted titles,
stepping aside for runs and syncs ([Runtime View](04-runtime.md#filldetails)).

**Housekeeping**: The nightly pass that trims old runs, usage and unused details and ids, and settles stored-twice
notes ([Cross-cutting Concepts](06-crosscutting.md#persistence-and-housekeeping)).

**Event, notification**: An event (`IEvent`) reaches only open pages and is never stored. A notification
(`INotification`) is a fact a person can subscribe to, delivered durably
([Cross-cutting Concepts](06-crosscutting.md#events-and-notifications)).

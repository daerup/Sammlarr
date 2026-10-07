# Split the ids from the services Sammlarr calls

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-05 |

## Context

`MediaProvider` named both a namespace of ids and a site Sammlarr calls, and capability flags on it generated the role
enums. A site that only owns ids (Rotten Tomatoes, Metacritic, Trakt) carried a capability matrix it never used; a
service that owns no ids (Wikidata) borrowed another provider's rate limit; and a new role meant a new flag, a new
generated enum and a matrix row, with the readiness of a role answered differently in each place that asked.

## Decision

`MediaProvider` names id namespaces and nothing else. `RemoteService` is one per rate limit: it answers its `Access`
(`Open`, a key slot, a sign-in or an app instance) and its `OwnIds`, and owns one capacity, one wait budget and one
brand. `Service` (a remote service, the Radarr + Sonarr pair, or Sammlarr accounts) is what serves a role. Every role
enum (`FeedProvider`, `TrendingProvider`, `SearchProvider`, `SinkKind`, `AuthProvider`) and every key slot is written by
hand and answers its `Service`; a module registers its clients, key checks, roles, converters and enrichers through
`ForRemote<TOptions>(RemoteService)`, and `IServiceReadiness` answers "is it connected" once, from the access alone.
Rating sources keep `[SuppliedBy<RemoteService>]`, and each rating type states its owner and whether it publishes
votes behind `IOwnedRating<TSelf>`.

Rejected: keeping the capability matrix with more flags (every role change edits a generator); one enum for both ids
and services (Wikidata and the IMDb datasets are services without ids, Metacritic is ids without a service).

## Consequences

* A new service is an enum member, its `Access` and `OwnIds` arms, its brand and a module; the compiler (CS8509 is an error) finds every switch it must join.
* The role registration tests hold every role member to a registration by the service it claims, and every remote service to a client.
* Capacity, waiting and outage state are per remote service, so a blocked MDBList never stops TMDB, and Wikidata no longer borrows Rotten Tomatoes' limit.
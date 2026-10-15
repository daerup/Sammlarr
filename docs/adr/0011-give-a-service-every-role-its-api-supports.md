# Give a service every role its API supports

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-05 |

## Context

Sammlarr leaned on TMDB and Plex for everything: a blocked TMDB, a missing key or a person without Plex left titles
without ids, details or ratings, and a show reached Sonarr only through TMDB's TVDB id. Each new provider was added for
one role, so there was no second path when the first one failed.

## Decision

Each service gets every role its API supports: a feed when it serves lists, an id converter when it maps ids, a
details enricher when it describes titles, and a rating supplier when it rates them. The id planner and the enricher
choice pick among them by cost, capacity and what is missing, so the cheapest open path wins and a blocked or missing
service has an alternative. Keyless sources (Wikidata, TVmaze, the IMDb datasets, the anime id map, AniList) are always
connected; datasets that are cheaper to download whole than to ask per title load daily into indexed tables.

Rejected: a fixed chain (TMDB, then Plex, then nothing), which keeps the single points of failure; one "best"
provider per role, which leaves the gaps a blocked or keyless install hits.

## Consequences

* A title with only an IMDb id still reaches TVDB through MDBList, Wikidata or TVmaze while TMDB is blocked or keyless, and gets IMDb's rating with votes from the daily file on every install.
* New rating sources count in every strategy at once, so a feed with a rating threshold can import or drop titles when one ships; the release note says so.
* Values from a source that cannot vouch for them (a title that may not be English, artwork no resizer can size) only fill gaps (`IDetailsEnricher.Defers`), so an extra source never makes a title worse.
* Every path is bounded: a lookup is asked once per input in a resolve, never by an id its own answer gave, and a title nobody can match is cached as unmatched for an hour.
# 1. Introduction and Goals

Sammlarr is a self-hosted Blazor Server app that turns lists and charts from media services into one wantlist per
person, synced with Plex or with Radarr and Sonarr. It keeps no catalogue: titles, ids, details and ratings come from
the services in [Context and Scope](02-context-scope.md). Sammlarr stores the curation plus a cache of what it looked
up. The idea to take away: a list entry becomes a title only through its ids, and an entry whose ids do not lead
to exactly one title is left out, never guessed.

Several people share one instance, signing in with Plex or with Sammlarr's own accounts (one of the two, chosen at
setup), each with their own feeds, wantlist, blocklist and API keys.

## Requirements Overview

The feature areas: pages under `UI/Features`, Settings sections under `UI/Features/Settings/Components`.

| Feature              | What it does                                                                                                                                                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Setup                | First-run wizard: setup code from the server log, sign-in method, sink, API keys, people.                                                                                                                                             |
| Home                 | Where the library came from, queued jobs and scheduled runs, recent activity by day.                                                                                                                                                  |
| Browse               | TMDB's trending movies and shows, to add to the wantlist.                                                                                                                                                                             |
| Search               | Finds a title through the chosen search provider (TMDB, Plex, or Radarr + Sonarr) and adds it.                                                                                                                                        |
| Feeds                | A list or chart from MDBList, TMDB, Trakt, AniList or MyAnimeList, filtered, sorted and limited into the wantlist, by hand or on a schedule. Append only adds; Mirror also removes what left the list. A live preview, a run history. |
| Live run view        | A running feed shows each title as it is matched, the service it waits on, and a tally of outcomes.                                                                                                                                   |
| Wantlist             | Synced with the sink: remote changes pulled, local ones pushed through an outbox. Each title shows its push status and origin (a feed, by hand, or the sink).                                                                         |
| Blocklist            | Titles a feed never adds.                                                                                                                                                                                                             |
| Ratings              | The Sammlarr rating: the first available source in a chosen order, or an average. A default per person; a feed may set its own and filter or sort by it.                                                                              |
| Notifications        | Webhook, Pushover and in-app messages for syncs, feed runs and rate limits, templated per subscription.                                                                                                                               |
| Providers            | Each person's API keys and Plex account; the admin's sink, search provider, Radarr and Sonarr.                                                                                                                                        |
| Users                | Admin and Member roles; open or closed Plex sign-ups, or invite and reset links for Sammlarr accounts.                                                                                                                                |
| Housekeeping (admin) | The nightly trim, the daily dataset downloads, titles stored twice to review.                                                                                                                                                         |
| Usage (admin)        | Calls per service, how they ended and what made them.                                                                                                                                                                                 |
| Diagnostics (admin)  | A live in-memory log stream with a zip export.                                                                                                                                                                                        |
| About                | The running version, a newer release if there is one, the data sources.                                                                                                                                                               |

## Quality Goals

Most important first. Each is a top node of the quality tree in [Quality Requirements](07-quality.md).

| Goal | Motivation |
|---|---|
| Correctness of list building | A wrong match quietly corrupts the library; a run that did not finish must not drop titles. |
| Resilience of provider calls | Services rate-limit and fail. A 429 storm or a lookup outage slows a run and leaves titles unmatched, never dropped; only a list that cannot be read fails the run. |
| Per-user data isolation | People share one instance. Each sees and changes only their own data. |
| Maintainability | One person evolves the code. Strong typing, strict layering, source generators and analyzers keep that tractable. |

## Stakeholders

| Role | Expectation |
|---|---|
| Admin | Whoever completes setup with the setup code, or a member an admin promotes in Settings › Users. Picks the sign-in method and sink, sets up Radarr and Sonarr, manages people; alone sees Users, Housekeeping, Usage and Diagnostics. Curates like a member. |
| Member | Joins by an invite link, or with Plex by signing in while sign-ups are open. Curates their own lists with their own API keys. |
| Maintainer | The single developer who builds and runs Sammlarr; wants a low-ceremony, documented architecture. |

The significant technical choices are ADRs in the [decision log](../../adr/README.md).

# Pace remote calls on lanes with limits as published

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-10 |

## Context

A feed load asks several services per title, and each publishes its own limit: TMDB about 40 requests a second, Trakt
1,000 per 5 minutes, MDBList 1,000 per 5 minutes on fixed windows plus a daily quota, AniList 30 a minute while it runs
degraded. A per-service concurrency gate, a rate limiter in requests per second and a cap of 100 titles in flight let
one blocked service fill every title slot, sent retries outside the pacing and slowed small runs for nothing. How should
Sammlarr pace its calls so that a blocked service slows only the titles that need it?

## Decision

Every call to a remote service passes a lane: a `LaneGate` per remote service, route (one of the provider's configured
URLs plus a path) and owner (an API key, an app's address). A lane holds the provider's limits as published, as
requests per window (sliding, or aligned to the provider's windows), and an optional cap on requests in flight. It
admits attempts by class (`Interactive`, `Source`, `Title`, `Background`) and then by order. Every HTTP attempt takes a
permit, retries included, and that retry is the only retry layer. A 429 with `Retry-After` blocks the lane, one without
halves its rate, and repeated failures take it down for a while until one probe succeeds. A quota (`ILaneQuota`) can
hold one class back while it admits another. An attempt waits for a block only within its service's `MaxWait`;
otherwise the lane refuses it before anything is sent. A service that answers many ids in one request also gets a keyed
lane, which sends one batch per batch key at a time and gathers the next while it is out, with no timer. Nothing in
Core caps titles or sleeps.

Rejected: one requests-per-second rate for every limit (it flattens a window, so it either slows small runs or allows
more than the window); a concurrency gate per service held through the retries (a request keeps its slot while it
sleeps out a block); a cap on titles in flight (a blocked service fills it, and titles that never need that service
wait); one circuit breaker per client (Plex's search, metadata and watchlist hosts would share one outage); a timer
that gathers each batch (it delays every title, and the batch in flight already gathers the next); a per-title deadline
(the lanes' own pacing would trip it on a long list); adaptive limiters such as Vegas or gradient (`Retry-After` is the
sharper signal).

## Consequences

* A blocked or down service holds back only its own lanes: titles that don't need it settle, and a search typed during
  a run goes ahead of the run's titles.
* A run that stays inside a window goes at full speed; pacing starts only when it would exceed it.
* A slow or throttled service gets bigger batches, not more of them.
* Limits live per lane in `appsettings.json`. A provider that publishes none (Plex) gets a setting chosen from the 429s
  it sends, not a promise.
* A refused attempt is never sent and costs nothing in usage. It reaches the caller as an `Unavailable` answer, so the
  title settles couldn't look up and nothing is cached as not found.
* With no per-title deadline, a load is bounded by `MaxWait`, the attempt timeout and its retries, and the run
  watchdog's `StallAfter`.

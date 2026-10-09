# Live runs pull from an in-memory trace

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-08 |

## Context

A feed run of 2,000 titles takes minutes, and while it runs nothing shows which title is waiting on which service: a
Plex rate limit, a 429 from TMDB or a slow details call look the same as a stuck run. The owner wants to watch the run
move. A run asks services hundreds of times a second at its peak. The app already runs OpenTelemetry traces over the
`Sammlarr` source, and pages hear about job state through `IEventPublisher`, which calls every subscriber synchronously
on the publishing thread.

## Decision

A run records its titles, paths, hops and give-ups as spans on a dedicated `Sammlarr.FeedRun` activity source, through
the static `FeedRunSpans`, and only inside the run span `FeedRunEngine` opens. A singleton `FeedRunTrace` reads them
with its own `ActivityListener` and keeps numbered steps per feed in memory: 30 seconds after the run ends, at most
50,000 steps, for the feed's owner only. Pages pull: a reader per circuit (`FeedRunProgress`) asks for the steps after
its cursor every 250 ms while one of its owner's feeds runs, and the browser replays each batch at its recorded timing,
250 ms behind. A late joiner starts from a snapshot. Every saved number (the card's bar, the run log) comes from the run
`FeedRunEngine` records; the trace is only drawn.

Rejected: hops as `IEvent`s on `IEventPublisher` (synchronous multicast on the run's own thread would slow the run, and
every page would render per hop); an OpenTelemetry exporter or SDK processor as the reader (exporters see ended spans in
one-second batches, after the sampler, and the view would break the day traces are sampled down or the SDK is turned
off); our own trace types fed by hand-written calls beside the spans (a second record of what the spans already say, and
the dashboard would not show it); a stored trace for replaying old runs (the saved run's counts carry what a past run
needs to show).

## Consequences

* The dashboard shows the same span tree as the view, since `TracingPipeline` adds the source for export.
* A restart or a run that ends while nobody looks loses its trace; the saved run is unaffected.
* Telemetry may drop spans and so may the view: a capped trace drops hop steps first and keeps the titles' fates.
* The cost is one listener callback per span start and stop, about 20,000 spans for a run of 2,000 titles, and four
  in-memory reads a second per watching circuit.

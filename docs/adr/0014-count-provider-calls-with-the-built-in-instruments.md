# Count provider calls with the built-in instruments

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-07 |

## Context

An admin cannot see how many calls Sammlarr makes to TMDB, Plex or MDBList, how they end or what made them. The calls
already pass two instruments: Polly's standard resilience handler records one `resilience.polly.pipeline.duration`
measurement per call, and HttpClient one `http.client.request.duration` measurement per attempt. The app already runs
the OpenTelemetry SDK for traces.

## Decision

Sammlarr tags those two instruments and keeps their numbers. A client built with `AddRemoteHttp` stamps each request
with its service and the ambient cause (`UsageCauses.Mark` at each entry point, the outermost mark winning) before the
retry, and registers an `HttpMetricsEnrichmentContext` callback on every attempt inside it; a `MeteringEnricher` adds
the call's outcome from its final status, its exception and the number of 429s the retry saw. A
`PeriodicExportingMetricReader` with delta temporality hands each minute to `UsageExporter`, which folds it into hourly
rows kept 90 days.

Rejected: reading the HTTP spans (a host to service map to keep, sampling undercounts, gone on restart); counting in
our own handler into our own counters (a second count of what the instruments already measure); counting only in memory
(gone on every restart); baggage for the cause (it would ride the W3C `baggage` header out to every service).

## Consequences

* About three measurements per call at roughly 0.3 µs each; the busiest hour costs a few milliseconds of CPU.
* Tags must stay bounded: a run id or a title as a tag would grow the SDK's tag combinations without limit, so a run's calls are credited through its feed id, and only calls marked `FeedRun` are credited: the feed editor marks its calls with the feed too, but a preview never joins that feed's running run.
* A retry sends a clone of the request, so the attempt tags are registered inside the retry; the stamp rides the request's options, which the clone keeps.
* With OTLP set, the dashboard shows the same meters.

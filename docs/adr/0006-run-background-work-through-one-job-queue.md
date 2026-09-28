# Run background work through one job queue

| Status   | Date       |
|----------|------------|
| accepted | 2026-09-24 |

## Context

Feeds need to run unattended on a cron, and a burst of scheduled runs should end in one watchlist
sync, not one per run. Something has to decide what runs, in what order, as which user, and the UI
has to be able to show it. Wolverine was already durable on the same SQLite file.

## Decision

One `Jobs` table is the queue of record. TickerQ holds the per-feed cron schedule and only
enqueues; one hosted `JobRunner` takes the oldest ready row and runs it in its own scope as the
row's owner, one job at a time. Enqueueing coalesces by kind, key and owner, and a sync armed by a
scheduled run is deferred by a grace period that later runs extend, up to a cap, so N runs coalesce
into one sync. Recovery needs no lease: with one runner in one process, any row still `Running`
at startup was abandoned, so startup requeues it.

Rejected: Wolverine as the queue (durable and has sequential queues, but it cannot upsert a pending
message by key, which is exactly the coalescing, and its envelope tables are internals the UI
should not render); an in-memory run lane plus a per-user timer for the sync (three separate
mechanisms, none of them queryable, all lost on restart); a lease column with expiry (only needed
with more than one runner).

## Consequences

* The queue is plain data: the Home dashboard reads it, and tests assert on rows.
* One job at a time keeps SQLite to one writer and each provider to one caller, but a hung job stalls everything behind it, and there is no per-job deadline yet.
* Startup recovery is only correct with one process, which ties this to single-instance deployment ([ADR 0004](0004-store-all-state-in-one-sqlite-file.md)).
* A handler that throws ends `Failed` with no automatic retry; the next trigger is the retry.

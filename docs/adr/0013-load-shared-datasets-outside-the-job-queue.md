# Load shared datasets outside the job queue

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-06 |

## Context

IMDb's ratings file and Fribb's anime id map are downloaded daily and replace a table that belongs to nobody. The
`Jobs` queue (ADR 0006) runs work as a person: every row has an owner, a foreign key to that user and a run in that
user's scope, and an install has no system user to own app-wide work.

## Decision

Each `IDatasetLoader` gets a daily TickerQ cron ticker, and a one-off ticker on a start before its first load, both
registered by `DatasetSchedule` once the host has started (TickerQ registers its functions in its own hosted service).
The `LoadDataset` ticker function runs the loader directly: TickerQ runs one ticker at a time, the loader asks with the
last `ETag`, replaces the table in one transaction through the context's connection and records the load in
`DatasetLoads`. Until a dataset's first load, its enricher or converter answers `Error`, so nothing stores an empty
answer for it.

Rejected: an owner picked from the admins (a deleted admin would cascade the queue rows away), a system user (a new
identity concept for two daily jobs), loading at start only (a long-running install would never refresh).

## Consequences

* ADR 0006 still holds for everything a person starts or schedules; shared datasets are the one exception, and they hold SQLite's write lock for a few seconds a day.
* A failed or empty file rolls back, so the last good table stays; a missed day is caught up by the next.
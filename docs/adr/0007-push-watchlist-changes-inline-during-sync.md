# Push watchlist changes inline during sync

| Status   | Date       |
|----------|------------|
| accepted | 2026-09-26 |

## Context

Local watchlist changes wait in an outbox until they reach Plex. The push used to be one Wolverine
`WatchlistPushRequest` message per item with its own retry policy, which pushed as soon as it could.
With scheduled feed runs that is wrong: a change should leave the machine when the sync says, so
opposing changes from different feeds can still cancel, and the sync should be able to report what
actually reached Plex.

## Decision

The outbox stays; the message goes. The sync engine drains pending outbox rows inline, as one batch
per action, after applying the remote pull. A delivered item is dropped, a failed one records an
attempt and waits for the next sync, and the fifth failure marks it `Failed`, which is terminal but
still counts as intent for the pull. Per-item status is published as in-process events.

Rejected: keeping the per-item message (pushes happen whenever the queue gets to them, so a feed's
add can reach Plex before another feed removes it, and the sync can only report what it queued); a
separate drain triggered on its own schedule (a push without the pull, which is not a sync).

## Consequences

* Opposing adds and removes cancel in the outbox before anything is sent.
* The sync notification reports what reached Plex, because the engine holds every ack before it publishes.
* A failed item is retried by the next sync rather than on a timer, so retries only happen as often as syncs do.
* Wolverine carries only notifications now.

# Separate events from notifications

| Status   | Date       |
|----------|------------|
| accepted | 2026-09-27 |

## Context

Six overlapping mechanisms carried "something happened" to users: two in-process buses, the
durable Wolverine publisher, the WebUi toast channel, and two announcer classes fanning out to them.
Both kinds were named `*Event`, so nothing told a reader or the compiler whether a fact was
durable. A feed-run notification was published but never reached a page.

## Decision

Two disjoint kinds, one transport each. An event (`IEvent`) is in-process and ephemeral, published
through `IEventPublisher` to open pages. A notification (`INotification`) is durable and
subscribable, queued on Wolverine and delivered by its channels, including the WebUi channel that
shows it as an in-app toast. Producers call one `Announcer`, whose two methods take the two
markers, so the wrong transport does not compile. The kind is a property of the fact: it is a
notification if a user could reasonably subscribe to it, whatever triggered it.

Rejected: a second in-process bus for notifications (duplicated the WebUi channel that already
delivers them to pages); Wolverine as the only bus (would persist hundreds of per-item events per
sync, and needs one bridge handler per notification type); choosing the kind by trigger (doubles
the types to express what both transports already gate on: no open page, or no subscription).

## Consequences

* A user who unsubscribes the WebUi target from a notification type stops getting that toast, even for an action they started.
* A toast waits on a durable write, which is fine per operation and why events stay off Wolverine.
* WebUi subscriptions are seeded when a user is created, so a type added to the seed later needs a backfill for existing users.

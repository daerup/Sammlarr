# Enforce per-user data isolation

| Status   | Date       |
|----------|------------|
| accepted | 2026-09-06 |

## Context

The app is multi-user: each user's feeds, watchlist and blacklist must be invisible to other
users, and it must be hard to leak data by forgetting a filter on a single query.

## Decision

Enforce isolation in the data layer, not per query. EF Core global query filters scope every read
to the current user (`IUserScope`); a `UserOwnershipStamper` stamps the owner on writes; the
current-user lookup is fail-closed, so a query with no authenticated user throws rather than
returning everyone's rows. Background work runs under an explicit current-user sub-scope.

Rejected: a manual `Where(x => x.UserId == current)` on every query (one forgotten filter leaks
data); trusting the UI to only request the user's own rows (unsafe).

## Consequences

* Isolation holds by default: a new query is scoped without the author doing anything.
* A missing or unauthenticated user fails closed and throws, it never opens the whole table.
* Ownership is stamped centrally on save, so writes cannot be mis-attributed.
* Background and async work has no ambient HTTP user, so it must establish a user scope explicitly; this is a required step, not optional.
* Isolation integration tests guard the invariant.

# Store all state in one SQLite file

| Status   | Date       |
|----------|------------|
| accepted | 2026-09-06 |

## Context

The app needs a datastore for application data and a durable queue for the Wolverine outbox. As a
self-hosted app, simplicity of deployment and backup matters more than scale.

## Decision

Use one SQLite file for everything: application data and the Wolverine message queue share the
same database. No external database server and no message broker.

Rejected: PostgreSQL plus a broker (operationally heavier than the app warrants today); a separate
store for the queue (a second thing to run and back up).

## Consequences

* Deployment is one process and one file (plus its data-protection keys) on a single `/data` volume, trivial to back up.
* Single instance only: a second replica would contend on the one file, so there is no horizontal scale-out today, and the `ProviderConfig` cache has no cross-instance invalidation.
* Postgres stays an open future. If multi-instance or heavier load is wanted, moving to PostgreSQL (with Wolverine's Postgres transport or a real broker) is the expected path and would supersede this ADR. Data access sits behind repositories and EF Core, which keeps that change bounded, though the migrations would be regenerated for the new provider.

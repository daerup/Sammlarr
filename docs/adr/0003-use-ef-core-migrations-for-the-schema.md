# Use EF Core migrations for the schema

| Status   | Date       |
|----------|------------|
| accepted | 2026-09-06 |

## Context

The database schema needs versioned, repeatable evolution across development, tests and deploy,
with seed data, and it must not silently drift from the code model.

## Decision

Use EF Core migrations. `Database.Migrate()` runs on startup in production and in tests (never
`EnsureCreated`); seed data ships through `HasData`; a `HasPendingModelChanges` guard fails the
build when the model drifts from the committed migrations.

Rejected: FluentMigrator (adopted, then reverted, it restated a schema the EF model already
describes and split schema truth in two); `EnsureCreated` (no migration history, cannot evolve a
live database).

## Consequences

* One source of schema truth: the EF model plus its generated migrations.
* Tests and production take the same migrate-on-start path, so they cannot diverge.
* A model change must be followed by `dotnet ef migrations add` or the pending-changes guard fails the build.
* Commits the data layer to EF Core.

## Notes

* 2026-10-04: the history was reset to one `Baseline` migration generated from the model as it stood, and the
  install's databases were deleted (nothing was in production; E2E seeds a new file per run). The 40 migrations before it, from `InitialCreate`
  (2026-07-27) to `KeyPromptedAt` (2026-10-04), live in git up to the commit before the reset, together with the
  tests that replayed their data rewrites. A schema compared through `pragma_table_info`, `pragma_index_list` and
  `pragma_foreign_key_list` matched the old chain's, except column order and the defaults the old `AddColumn`
  calls had added to fill existing rows.

# Use EF Core migrations for the schema

| Status   | Date       |
|----------|------------|
| accepted | 2026-09-06 |

## Context

The database schema needs versioned, repeatable evolution across development, tests and deploy,
with seed data, and it must not silently drift from the code model.

## Decision

Use EF Core migrations. `Database.Migrate()` runs on startup in production and in tests (never
`EnsureCreated`); seed data ships through the context's seeding hooks (through `HasData` until 2026-10-05, see
Notes); a `HasPendingModelChanges` guard fails the
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
* 2026-10-05: the provider-config row left `HasData` for the context's `UseSeeding` and `UseAsyncSeeding` hooks,
  which insert it after a migrate only when it is missing. The row is edited at runtime, and `HasData` would turn any
  later change to its defaults into an `UpdateData` that resets every install's settings. `Baseline` no longer inserts
  the row and `DropProviderConfigSeed` carries only the snapshot change, so existing databases keep their row and a
  new one gets `ProviderConfigRow.Default` from the hooks.
* 2026-10-16: the history was reset again to one `Baseline` before the first release, and every database was
  deleted. The 37 migrations before it, from the 2026-10-04 `Baseline` to `LinksAsIdentityTokens`, live in git up to
  the commit before the reset, with the two tests that replayed them. The new schema matched the old chain's in every
  table, column, index and foreign key. From the first release on, migrations are only added, never changed or
  deleted, since installs keep the history they ran.

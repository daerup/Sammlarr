# Architecture Decision Log

This folder records significant architecture decisions as
[MADR](https://adr.github.io/madr/) files, one Markdown file per decision.

## How to add an ADR

1. Copy [`0000-template.md`](0000-template.md) to the next free number:
   `NNNN-imperative-verb-phrase.md` (lowercase, dash-separated).
2. Fill it in: status, context, drivers, considered options, outcome, consequences.
3. Add a row to the log below.

Decisions are append-only. To change one, add a new ADR and mark the old one
`superseded by` the new number rather than editing history.

## Log

| # | Title | Status | Date |
|---|-------|--------|------|
| 0001 | [Drive deployment from the Aspire AppHost](0001-drive-deployment-from-the-aspire-apphost.md) | superseded by [0021](0021-ship-one-hand-written-compose-file.md) | 2026-09-02 |
| 0002 | [Split presentation from the host](0002-split-presentation-from-the-host.md) | accepted | 2026-09-06 |
| 0003 | [Use EF Core migrations for the schema](0003-use-ef-core-migrations-for-the-schema.md) | accepted | 2026-09-06 |
| 0004 | [Store all state in one SQLite file](0004-store-all-state-in-one-sqlite-file.md) | accepted | 2026-09-06 |
| 0005 | [Enforce per-user data isolation](0005-enforce-per-user-data-isolation.md) | accepted | 2026-09-06 |
| 0006 | [Run background work through one job queue](0006-run-background-work-through-one-job-queue.md) | accepted | 2026-09-24 |
| 0007 | [Push watchlist changes inline during sync](0007-push-watchlist-changes-inline-during-sync.md) | accepted | 2026-09-26 |
| 0008 | [Separate events from notifications](0008-separate-events-from-notifications.md) | accepted | 2026-09-27 |
| 0009 | [Model a title as an identity plus optional details](0009-model-a-title-as-an-identity-plus-optional-details.md) | accepted | 2026-10-03 |
| 0010 | [Split the ids from the services Sammlarr calls](0010-split-the-ids-from-the-services-sammlarr-calls.md) | accepted | 2026-10-05 |
| 0011 | [Give a service every role its API supports](0011-give-a-service-every-role-its-api-supports.md) | accepted | 2026-10-05 |
| 0012 | [Read IMDb, Trakt and Letterboxd lists through MDBList](0012-read-imdb-trakt-and-letterboxd-lists-through-mdblist.md) | accepted, superseded for Trakt | 2026-10-05 |
| 0013 | [Load shared datasets outside the job queue](0013-load-shared-datasets-outside-the-job-queue.md) | accepted | 2026-10-06 |
| 0014 | [Count provider calls with the built-in instruments](0014-count-provider-calls-with-the-built-in-instruments.md) | accepted | 2026-10-07 |
| 0015 | [Keep an id two stored titles claim on neither](0015-keep-an-id-two-stored-titles-claim-on-neither.md) | superseded by [0018](0018-claim-identity-before-hand-out-from-identity-keys-only.md) | 2026-10-08 |
| 0016 | [Live runs pull from an in-memory trace](0016-live-runs-pull-from-an-in-memory-trace.md) | accepted | 2026-10-08 |
| 0017 | [Pace remote calls on lanes with limits as published](0017-pace-remote-calls-on-lanes-with-limits-as-published.md) | accepted | 2026-10-10 |
| 0018 | [Claim identity before hand-out from identity keys only](0018-claim-identity-before-hand-out-from-identity-keys-only.md) | accepted | 2026-10-10 |
| 0019 | [Build and push the image with Docker's actions, compose with Aspire](0019-build-and-push-the-image-with-docker-actions.md) | superseded by [0023](0023-push-an-unsigned-image-by-version-and-move-latest-last.md) | 2026-10-13 |
| 0020 | [Move local accounts onto ASP.NET Core Identity](0020-move-local-accounts-onto-asp-net-core-identity.md) | accepted, limits per address superseded by [0022](0022-rate-limit-requests-in-the-app.md) | 2026-10-16 |
| 0021 | [Ship one hand-written compose file](0021-ship-one-hand-written-compose-file.md) | accepted | 2026-10-16 |
| 0022 | [Rate-limit requests in the app](0022-rate-limit-requests-in-the-app.md) | accepted | 2026-10-16 |
| 0023 | [Push an unsigned image by version and move latest last](0023-push-an-unsigned-image-by-version-and-move-latest-last.md) | accepted, attestation superseded by [0025](0025-attest-the-image-provenance-outside-the-registry.md), version tag at push by [0026](0026-publish-a-release-only-after-its-checks-pass.md) | 2026-10-17 |
| 0024 | [Build the image for amd64 and arm64 on one runner](0024-build-the-image-for-amd64-and-arm64-on-one-runner.md) | accepted | 2026-10-17 |
| 0025 | [Attest the image provenance outside the registry](0025-attest-the-image-provenance-outside-the-registry.md) | accepted | 2026-10-17 |
| 0026 | [Publish a release only after its checks pass](0026-publish-a-release-only-after-its-checks-pass.md) | accepted | 2026-10-17 |

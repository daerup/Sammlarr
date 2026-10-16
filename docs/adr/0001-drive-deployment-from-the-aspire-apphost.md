# Drive deployment from the Aspire AppHost

| Status                                                                               | Date       |
|--------------------------------------------------------------------------------------|------------|
| accepted                                                                             | 2026-09-02 |
| push path superseded by [0019](0019-build-and-push-the-image-with-docker-actions.md) | 2026-10-13 |
| superseded by [0021](0021-ship-one-hand-written-compose-file.md)                     | 2026-10-16 |

## Context

The Aspire AppHost was a dev-only launcher; deployment was a hand-written `compose.yaml`
disconnected from it, building from a `UI/Dockerfile` that did not exist. Two definitions of
"how to run", and no working deploy path.

## Decision

The AppHost is the source of truth (full lean-in). Run mode launches the UI on `:6769`. Publish
mode generates the compose artifact, builds the image from release-engineering's hand-written
`Web/Dockerfile`, and pushes to GHCR. A custom `ServiceDefaults` adds health checks only (not the
stock OpenTelemetry export or global resilience, which the app already owns via Serilog and
per-provider pipelines). OTLP stays env-driven and the dashboard is dev-only.

Rejected: Aspire as a dev launcher only (leaves the deploy artifact disconnected); Aspire
generating compose but referencing a prebuilt image (splits image ownership).

## Consequences

* Run and deploy come from one model, so they cannot drift.
* Keeps the scannable Dockerfile (Aspire builds from it, not the SDK container build), so SBOM and signing survive.
* State and secrets persist via one `/data` volume (keys persisted with a stable app name).
* Revises release-engineering's boundary: its build-push is replaced by `aspire do push`; cosign and SBOM attach to the Aspire-pushed image.
* `aspire publish` was blocked until release-engineering authored `Web/Dockerfile`, which makes `/data` writable by the container user.

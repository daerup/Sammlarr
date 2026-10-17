# Push an unsigned image by version and move latest last

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-17 |

## Context

ADR 0019 had `release.yml` push each release as `:X.Y.Z`, `:latest` and `:sha-…` at once, then sign its digest with
cosign and attest its provenance and both SBOMs. GHCR has no OCI referrers API, so the signature and the three
attestations went into an index tagged `sha256-<digest>`, which the package page lists as a second version beside every
release. `:latest` also moved before the signing and the rebuild check had passed, and `:sha-…` always named the same
image as the version tag.

## Decision

`release.yml` builds and pushes `ghcr.io/daerup/sammlarr` with Docker's actions (`.github/actions/build-image`,
`rewrite-timestamp`, the GHA cache) and keeps the digest, as before. The build pushes `:X.Y.Z` only. A last job,
`latest`, moves `:latest` to that digest with `docker buildx imagetools create --prefer-index=false` once the `rebuild`
job matched it. Releases are neither signed nor attested; each carries the two SBOMs and `THIRD-PARTY-NOTICES.txt` as
release assets.

Rejected: keeping cosign and the attestations (one more `sha256-<digest>` version beside every release on the package
page); keeping the `:sha` tag (every image comes from a version tag, so it always named the same image).

## Consequences

* The package page lists one version per release, tagged with its version and, for the newest, `:latest`.
* `:latest` reaches the people who follow it only after the rebuild matched the pushed digest.
* Nothing but the rebuild check vouches for an image: no `cosign verify`, no `gh attestation verify`, and Scorecard's
  Signed-Releases check finds no signed release asset.
* 0.1.0 keeps its signature, its attestations and its `sha-76a7894` tag.
* Supersedes ADR 0019, whose compose half ADR 0021 already replaced.

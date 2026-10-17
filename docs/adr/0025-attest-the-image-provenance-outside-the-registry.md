# Attest the image provenance outside the registry

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-17 |

## Context

ADR 0023 dropped cosign and the attestations: GHCR has no OCI referrers API, so each signature or attestation pushed
there showed as one more `sha256-<digest>` version on the package page. That left only the rebuild check to vouch for
an image, and Scorecard's Signed-Releases check, which looks for a signed asset on each release, at 0.

## Decision

The `image` job in `release.yml` attests the pushed index's build provenance with `actions/attest`, without
`push-to-registry`: the attestation is stored with the repository's attestations, not in GHCR, and its Sigstore bundle
goes on the release as `sammlarr-image.intoto.jsonl`. The image itself is still not signed with cosign. Before `:latest`
moves, the `notes` job verifies the attestation with `gh attestation verify`, held to `release.yml`, the tag and a
GitHub-hosted runner, and the release notes show the result beside the rebuild check's.

Rejected: cosign and `push-to-registry: true` (the extra package version ADR 0023 removed); signing the release assets
with `cosign sign-blob` (one bundle per asset, and nothing that names the image).

## Consequences

* `gh attestation verify oci://ghcr.io/daerup/sammlarr:X.Y.Z --repo daerup/Sammlarr` checks that a workflow of this
  repository built the image; the package page keeps one version per release.
* Each release carries one more asset, which Signed-Releases counts.
* The attestation names the index digest from outside the image, so the image and the rebuild check are unchanged.
* A release whose attestation does not verify keeps `:latest` on the previous release, and its notes say so.
* The `image` job needs `id-token: write` and `attestations: write` again, the `notes` job `attestations: read`.
* Supersedes the attestation half of ADR 0023.

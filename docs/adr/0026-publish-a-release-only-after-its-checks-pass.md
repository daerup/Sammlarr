# Publish a release only after its checks pass

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-17 |

## Context

release-please published each GitHub release when its release PR merged, before `release.yml` had built anything, and
the `image` job pushed the image as `:X.Y.Z` before the rebuild and the attestation were checked. A release whose build
failed stayed public without an image, and one whose checks failed stayed public with ❌ in its notes and its version
tag on GHCR; only `:latest` held back.

## Decision

release-please creates each release as a draft (`draft`) and pushes its tag itself (`force-tag-creation`), since GitHub
creates no tag for a draft and the tag is what starts `release.yml`. The `image` job pushes the index by digest only
(`push-by-digest=true`). The last job, `publish`, runs once the `rebuild` job matched the digest and the `notes` job
verified the attestation: it tags the digest `:X.Y.Z` and `:latest` with
`docker buildx imagetools create --prefer-index=false` and publishes the draft.

Rejected: publishing as soon as the image is pushed (a release with ❌ checks would still go out); pushing `:X.Y.Z`
with the build and holding back only `:latest` (a failed release would still leave its version tag on GHCR).

## Consequences

* A published release always has its image tagged, rebuilt to the same digest and attested.
* A failed release stays a draft with its notes and assets, and GHCR holds only its untagged index and platform images.
* The `publish` job needs `contents: write` beside `packages: write`; the `notes` job takes the newest published
  release as the previous one.
* release-please finds a draft as the latest release only through its tag, so `force-tag-creation` stays on with
  `draft`.
* Supersedes the version tag at push in ADR 0023.

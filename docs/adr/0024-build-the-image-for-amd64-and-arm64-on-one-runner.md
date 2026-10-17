# Build the image for amd64 and arm64 on one runner

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-17 |

## Context

Releases pushed `ghcr.io/daerup/sammlarr` for `linux/amd64` only, so an arm64 host such as a Raspberry Pi or an Apple
silicon Mac ran it under emulation or not at all. `src/Web/Dockerfile` already runs its build stages on the build
machine's platform and publishes with `-a $TARGETARCH`, `src/Web/Web.csproj` restores both `linux-x64` and
`linux-arm64`, and the runtime stage only copies files, so nothing in the build has to run on arm64.

## Decision

`.github/actions/build-image` builds `linux/amd64,linux/arm64` by default, on the one amd64 runner of each job, and
`release.yml` pushes the index over both as `:X.Y.Z`. The `rebuild` job builds both again without cache, compares each
platform's `/app` and fails unless the index digest matches. The two images hold the same OS packages at the same
versions, and only the .NET runtime packs' names differ, so the amd64 image stands for both elsewhere: the release's
one `sammlarr-image.cdx.json` and its `THIRD-PARTY-NOTICES.txt` are read from it, the Release PR's Trivy scan builds
`linux/amd64` only, and the weekly scan of `:latest` reads its amd64 image.

Rejected: building each platform on its own runner, GitHub's arm64 runner for arm64, and merging the two into an index
(a second build job and a merge step, for a build that needs no arm64 machine); emulating arm64 with QEMU (slow, and
nothing has to run there).

## Consequences

* `docker pull` and the compose file pick the image for the host's platform; the tags and the digest in the release
  notes name the index.
* A release publishes the app twice, one platform after the other: two restores into one shared NuGet folder race,
  so the Dockerfile's cache mounts are `sharing=locked`. The `image` and `rebuild` jobs take longer.
* Nothing runs the arm64 image before it ships; a package that only arm64 carries would go unscanned and unlisted.
* The image SBOM names the index digest but carries the amd64 image's ID and layers and marks its packages
  `arch=amd64`: for an arm64 pull it lists the right packages under the wrong architecture.
* 0.1.0 stays amd64 only.

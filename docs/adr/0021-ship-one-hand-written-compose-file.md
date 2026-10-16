# Ship one hand-written compose file

| Status   | Date       |
|----------|------------|
| accepted | 2026-10-16 |

## Context

ADR 0019 kept the last part of ADR 0001: the AppHost's publish branch wrote a compose file (`aspire publish`) that
each release attached, naming that release's image tag. Since the README's Quick start downloads the repo's own
`compose.yaml`, there were two compose files: the hand-written one with its comments, `TZ` and `Sammlarr__PublicUrl`,
and a generated one with none of them. Its only extra was the pinned tag.

## Decision

`compose.yaml` in the repo is the only compose file. Releases attach none; the AppHost has no publish branch and no
longer references `Aspire.Hosting.Docker`, and the release workflow no longer runs `aspire publish`. The AppHost only
starts Sammlarr for development, staging and the E2E stacks. Pinning a version means editing the image tag in
`compose.yaml`.

Rejected: keeping the generated file and adding the missing settings to the AppHost (two files to keep alike); a copy
of `compose.yaml` with the tag pinned attached to each release (a second file for what one edit does).

## Consequences

* One compose file to keep true, with the comments people read.
* A release's image tag is its version; `compose.yaml` follows `:latest`, and Renovate leaves that line alone.
* Supersedes ADR 0019's compose half and what was left of ADR 0001: Docker's actions build and push the image, and
  nothing generates deployment files from the AppHost.
* The `aspire.cli` local tool, used only by `aspire publish`, leaves the tool manifest.

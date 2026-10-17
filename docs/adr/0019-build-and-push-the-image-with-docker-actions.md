# Build and push the image with Docker's actions, compose with Aspire

| Status                                                                        | Date       |
|-------------------------------------------------------------------------------|------------|
| accepted                                                                      | 2026-10-13 |
| compose half superseded by [0021](0021-ship-one-hand-written-compose-file.md) | 2026-10-16 |

## Context

ADR 0001 let the AppHost build the image from `Web/Dockerfile` and push it to GHCR (`aspire do push`). The release
pipeline needs more from that step than Aspire 13.6 offers: BuildKit's `rewrite-timestamp` for builds that rebuild to
the same digest, a layer cache across runs, several tags and OCI labels, and the pushed digest that cosign and the
attestations sign. Aspire's push has none of these, and a compose it writes for a Dockerfile-built service carries an
image placeholder instead of the released image.

## Decision

`release.yml` builds and pushes `ghcr.io/daerup/sammlarr` with `docker/metadata-action` and `docker/build-push-action`
(`:X.Y.Z`, `:latest`, `:sha`, `rewrite-timestamp`, the GHA cache), and keeps the digest for signing. The AppHost's
publish branch names that image with `AddContainer` and only writes the compose (`aspire publish`, dashboard off),
which the release attaches. Run mode, the `/data` volume and the single Web process stay as ADR 0001 has them.

Rejected: `aspire do push` (no exporter options, no digest output, one tag); `dotnet publish -t:PublishContainer`
(less transparent to scan and attest than the Dockerfile). Revisit when Aspire's push gains exporter options and a
digest output.

## Consequences

* The released image and the compose a self-hoster runs name the same tag, so the compose works as it is.
* The image step adds nothing that changes between two builds of a tag (the commit time as `SOURCE_DATE_EPOCH`,
  `rewrite-timestamp`, the `created` label from the commit), so a rebuild reaches the same digest once the app build
  itself is reproducible; cosign and the attestations sign the digest that was pushed.
* The image build lives in a workflow, not the AppHost; `Web/Dockerfile` stays the one definition of the image.
* Supersedes ADR 0001's push path; the hand-written `compose.yaml` is gone.

## Notes

* 2026-10-16: `compose.yaml` is hand-written again and the only compose file; releases attach none
  ([ADR 0021](0021-ship-one-hand-written-compose-file.md)).
* 2026-10-17: the build pushes `:X.Y.Z` only; `:latest` moves in a last job, once the rebuild matched. The `:sha` tag
  is gone, since every image comes from a version tag. Releases are no longer signed or attested: GHCR has no referrers
  API, so each signature and attestation showed as one more tagged version beside the image.
